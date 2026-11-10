import { Suspense, useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { MaterialFormModalSlot } from '@/app/featureSlots';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import {
  filterNorms,
  newKey,
  colorOptions as toColorOptions,
  patchRow,
  type ComponentRow,
  type MaterialOption,
  type NormFilter,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { specNormColumns } from './specNormColumns';

const text = MESSAGES.orders.spec;

type SpecNormsTabProps = {
  norms: NormRow[];
  setNorms: Dispatch<SetStateAction<NormRow[]>>;
  variants: VariantRow[];
  components: ComponentRow[];
  materialOptions: MaterialOption[];
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
  const [addingMaterialFor, setAddingMaterialFor] = useState<string | null>(null);

  const colorOptions = useMemo(() => toColorOptions(variants), [variants]);
  // Chọn theo khóa dòng, nên đổi tên thành phần thì nhãn đổi theo mà dòng định mức không mất liên kết.
  const componentOptions = components.map((c) => ({
    value: c.key,
    label: c.componentName.trim() || MESSAGES.orders.specValidation.unnamedComponent,
  }));
  const componentLabel = (key: string | undefined) =>
    componentOptions.find((o) => o.value === key)?.label ?? '';
  // Thành phần đang lọc có thể đã bị xóa ở tab khác: bỏ lọc thay vì giữ một khóa không còn.
  const effectiveFilter: NormFilter = {
    ...normFilter,
    component: componentOptions.some((o) => o.value === normFilter.component)
      ? normFilter.component
      : undefined,
  };
  const materialLabel = (id: number | undefined) =>
    materialOptions.find((m) => m.value === id)?.label ?? '';
  const colorFilterOptions = colorOptions
    .filter((o) => o.label)
    .map((o) => ({ value: o.label, label: o.label }));
  const visibleNorms = filterNorms(
    norms,
    effectiveFilter,
    (r) => `${componentLabel(r.componentKey)} ${materialLabel(r.materialId)} ${r.specNote}`,
  );
  const changeFilter = (change: Partial<NormFilter>) => {
    setNormFilter((f) => ({ ...f, ...change }));
    setNormPage((p) => ({ ...p, current: 1 }));
  };
  const hasNormFilter = Object.values(effectiveFilter).some((v) => v !== undefined && v !== '');

  const addNorm = () => {
    setNormFilter({});
    setNormPage((p) => ({ ...p, current: Math.ceil((norms.length + 1) / p.pageSize) }));
    setNorms((rows) => [
      ...rows,
      {
        key: newKey(),
        componentKey: undefined,
        materialId: undefined,
        specNote: '',
        colorKeys: colorOptions.map((o) => o.value),
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
              value={effectiveFilter.component}
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
          colorOptions,
          setNorms,
          onAddMaterial: setAddingMaterialFor,
        })}
      />
      <Button type="dashed" icon={<PlusOutlined />} style={{ marginTop: 8 }} onClick={addNorm}>
        {text.addNorm}
      </Button>
      {addingMaterialFor && (
        <Suspense fallback={null}>
          <MaterialFormModalSlot
            open
            material={null}
            onClose={() => setAddingMaterialFor(null)}
            onSaved={(m) => patchRow(setNorms, addingMaterialFor, { materialId: m.materialId })}
          />
        </Suspense>
      )}
    </>
  );
}
