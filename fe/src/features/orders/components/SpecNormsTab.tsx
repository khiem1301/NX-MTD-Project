import { useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import { foldVietnamese } from '@/utils/colorSearch';
import {
  newKey,
  variantKey,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { specNormColumns } from './specNormColumns';

const text = MESSAGES.orders.spec;

type NormFilter = {
  keyword?: string;
  component?: string;
  materialId?: number;
  color?: string;
  size?: string;
};

type SpecNormsTabProps = {
  norms: NormRow[];
  setNorms: Dispatch<SetStateAction<NormRow[]>>;
  variants: VariantRow[];
  components: ComponentRow[];
  materialOptions: { value: number; label: string }[];
  materialsLoading: boolean;
};

/** Tab "Định mức NVL": mỗi dòng là một thành phần dùng NVL nào; một dòng áp dụng cho nhiều màu – size cùng lúc. */
export function SpecNormsTab({
  norms,
  setNorms,
  variants,
  components,
  materialOptions,
  materialsLoading,
}: SpecNormsTabProps) {
  const [normFilter, setNormFilter] = useState<NormFilter>({});
  const [normPage, setNormPage] = useState({ current: 1, pageSize: 10 });

  const variantOptions = useMemo(
    () =>
      variants.map((v) => ({
        value: variantKey(v.color, v.sizeCode),
        label: `${v.color || '?'} - ${v.sizeCode || '?'}`,
      })),
    [variants],
  );
  const componentOptions = components
    .filter((c) => c.componentName.trim())
    .map((c) => ({ value: c.componentName.trim(), label: c.componentName.trim() }));
  const materialLabel = (id: number | undefined) =>
    materialOptions.find((m) => m.value === id)?.label ?? '';
  const colorFilterOptions = [...new Set(variants.map((v) => v.color.trim()).filter(Boolean))].map(
    (c) => ({ value: c, label: c }),
  );
  const sizeFilterOptions = [
    ...new Set(variants.map((v) => v.sizeCode.trim()).filter(Boolean)),
  ].map((s) => ({ value: s, label: s }));
  const appliesTo = (r: NormRow, match: (v: VariantRow) => boolean) =>
    r.variantKeys.some((k) =>
      variants.some((v) => variantKey(v.color, v.sizeCode) === k && match(v)),
    );
  const visibleNorms = norms.filter((r) => {
    const f = normFilter;
    if (f.component && r.componentName !== f.component) return false;
    if (f.materialId !== undefined && r.materialId !== f.materialId) return false;
    if (f.color && !appliesTo(r, (v) => v.color.trim() === f.color)) return false;
    if (f.size && !appliesTo(r, (v) => v.sizeCode.trim() === f.size)) return false;
    const kw = foldVietnamese(f.keyword ?? '');
    return (
      !kw ||
      foldVietnamese(
        `${r.componentName ?? ''} ${materialLabel(r.materialId)} ${r.specNote}`,
      ).includes(kw)
    );
  });
  const changeFilter = (change: Partial<NormFilter>) => {
    setNormFilter((f) => ({ ...f, ...change }));
    setNormPage((p) => ({ ...p, current: 1 }));
  };
  const hasNormFilter = Object.values(normFilter).some((v) => v !== undefined && v !== '');

  const addNorm = () => {
    setNormFilter({});
    setNormPage((p) => ({ ...p, current: Math.ceil((norms.length + 1) / p.pageSize) }));
    setNorms((rows) => [
      ...rows,
      {
        key: newKey(),
        componentName: undefined,
        materialId: undefined,
        lengthCm: null,
        widthCm: null,
        specNote: '',
        variantKeys: variantOptions.map((o) => o.value),
      },
    ]);
  };

  return (
    <>
      <Typography.Paragraph type="secondary">{text.normHint}</Typography.Paragraph>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={text.normSearch}
              style={{ width: 260 }}
              onSearch={(k) => changeFilter({ keyword: k.trim() || undefined })}
            />
            <FilterSelect
              value={normFilter.component}
              placeholder={text.component}
              style={{ width: 190 }}
              options={componentOptions}
              onChange={(v) => changeFilter({ component: v })}
            />
            <FilterSelect
              value={normFilter.materialId}
              placeholder={text.material}
              style={{ width: 220 }}
              options={materialOptions}
              showSearch
              optionFilterProp="label"
              onChange={(v) => changeFilter({ materialId: v })}
            />
            <FilterSelect
              value={normFilter.color}
              placeholder={text.color}
              style={{ width: 150 }}
              options={colorFilterOptions}
              onChange={(v) => changeFilter({ color: v })}
            />
            <FilterSelect
              value={normFilter.size}
              placeholder={text.size}
              style={{ width: 130 }}
              options={sizeFilterOptions}
              onChange={(v) => changeFilter({ size: v })}
            />
            {hasNormFilter && (
              <span style={{ opacity: 0.65 }}>
                {text.normShown
                  .replace('{n}', String(visibleNorms.length))
                  .replace('{total}', String(norms.length))}
              </span>
            )}
          </>
        }
      />
      <DataTable<NormRow>
        rowKey="key"
        size="small"
        pagination={{
          ...normPage,
          total: visibleNorms.length,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total) => `${total} ${text.normRows}`,
          onChange: (current, pageSize) => setNormPage({ current, pageSize }),
        }}
        dataSource={visibleNorms}
        scroll={{ x: 'max-content', y: 'max(260px, calc(100vh - 560px))' }}
        columns={specNormColumns({
          componentOptions,
          materialOptions,
          materialsLoading,
          variantOptions,
          setNorms,
        })}
      />
      <Button type="dashed" icon={<PlusOutlined />} style={{ marginTop: 8 }} onClick={addNorm}>
        {text.addNorm}
      </Button>
    </>
  );
}
