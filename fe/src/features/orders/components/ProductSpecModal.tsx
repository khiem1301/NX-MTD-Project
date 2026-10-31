import { useEffect, useMemo, useRef, useState } from 'react';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Spin,
  Tabs,
  Typography,
} from 'antd';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { MESSAGES } from '@/constants/messages';
import { foldVietnamese } from '@/utils/colorSearch';
import { useMaterialOptions } from '@/hooks/useLookups';
import { useProduct, useProductSpec, useSaveProductSpec } from '../hooks/useOrders';
import type { ProductDetail, ProductListItem, ProductSpec } from '../types';
import {
  newKey,
  toPayload,
  toRows,
  validate,
  variantKey,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { AuditHistory } from '@/components/common/AuditHistory';
import { DetailModal, ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Props = { product: ProductListItem | null; showHistory: boolean; onClose: () => void };
type NormFilter = {
  keyword?: string;
  component?: string;
  materialId?: number;
  color?: string;
  size?: string;
};

const text = MESSAGES.orders.spec;
const TYPE_OPTIONS = [
  { value: 'Panel', label: text.typePanel },
  { value: 'Trim', label: text.typeTrim },
];

/**
 * UC10 (định mức): khai báo một lần cho mã hàng — thành phần (chi tiết cắt / phụ liệu), các biến thể màu × size và NVL của
 * từng thành phần. Một dòng định mức áp dụng cho nhiều màu – size cùng lúc; máy chủ lưu theo từng biến thể.
 */
export function ProductSpecModal({ product, showHistory, onClose }: Props) {
  const spec = useProductSpec(product?.productId);
  const detail = useProduct(product?.productId, showHistory);
  const dirty = useRef(false);

  /** Còn thay đổi chưa lưu thì hỏi lại trước khi đóng. */
  const requestClose = () => {
    if (!dirty.current) return onClose();
    Modal.confirm({
      title: text.unsavedTitle,
      content: text.unsavedBody,
      okText: text.leave,
      cancelText: text.stay,
      okButtonProps: { danger: true },
      onOk: onClose,
    });
  };
  return (
    <DetailModal
      open={product !== null}
      title={
        product ? `${text.title}: ${product.productCode} - ${product.productName}` : text.title
      }
      onClose={requestClose}
      size="xl"
    >
      {spec.error && (
        <Alert type="error" showIcon message={spec.error.message} style={{ marginBottom: 12 }} />
      )}
      {spec.isLoading && <Spin />}
      {product && spec.data && (
        <SpecEditor
          key={spec.dataUpdatedAt}
          product={product}
          spec={spec.data}
          detail={detail.data}
          showHistory={showHistory}
          onDirtyChange={(v) => (dirty.current = v)}
          onClose={requestClose}
        />
      )}
    </DetailModal>
  );
}

/** Phần soạn: khởi tạo từ dữ liệu máy chủ; lưu xong dữ liệu tải lại (đổi key) nên dựng lại từ bản đã lưu. */
function SpecEditor({
  product,
  spec,
  detail,
  showHistory,
  onDirtyChange,
  onClose,
}: {
  product: ProductListItem;
  spec: ProductSpec;
  detail: ProductDetail | undefined;
  showHistory: boolean;
  onDirtyChange: (dirty: boolean) => void;
  onClose: () => void;
}) {
  const save = useSaveProductSpec();
  const materials = useMaterialOptions();
  const [initial] = useState(() => toRows(spec));
  const [components, setComponents] = useState<ComponentRow[]>(initial.components);
  const [variants, setVariants] = useState<VariantRow[]>(initial.variants);
  const [norms, setNorms] = useState<NormRow[]>(initial.norms);
  const [error, setError] = useState('');
  const [normFilter, setNormFilter] = useState<NormFilter>({});
  const [normPage, setNormPage] = useState({ current: 1, pageSize: 10 });

  const snapshot = JSON.stringify({ components, variants, norms });
  const [baseline] = useState(snapshot);
  useEffect(() => {
    onDirtyChange(snapshot !== baseline);
    return () => onDirtyChange(false);
  }, [snapshot, baseline, onDirtyChange]);

  const patch = <T extends { key: string }>(
    set: (f: (rows: T[]) => T[]) => void,
    key: string,
    change: Partial<T>,
  ) => set((rows) => rows.map((r) => (r.key === key ? { ...r, ...change } : r)));

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
  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
  }));

  const handleSave = () => {
    const problem = validate(components, variants, norms);
    setError(problem);
    if (problem) return;
    save.mutate({ id: product.productId, body: toPayload(components, variants, norms) });
  };

  const discontinued = spec.components
    .filter((c) => c.status !== 'Active')
    .map((c) => c.componentName);
  const discontinuedVariants = spec.variants
    .filter((v) => v.status !== 'Active')
    .map((v) => `${v.color}-${v.sizeCode}`);

  const delCol = <T extends { key: string }>(set: (f: (rows: T[]) => T[]) => void) => ({
    title: '',
    width: 48,
    fixed: 'right' as const,
    render: (_: unknown, r: T) => (
      <Button
        type="text"
        danger
        icon={<DeleteOutlined />}
        aria-label={MESSAGES.common.delete}
        onClick={() => set((rows) => rows.filter((x) => x.key !== r.key))}
      />
    ),
  });

  const componentTab = (
    <>
      <Typography.Paragraph type="secondary">{text.componentHint}</Typography.Paragraph>
      <DataTable<ComponentRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={components}
        columns={[
          {
            title: text.componentName,
            render: (_, r) => (
              <Input
                size="small"
                value={r.componentName}
                maxLength={100}
                onChange={(e) => patch(setComponents, r.key, { componentName: e.target.value })}
              />
            ),
          },
          {
            title: text.componentType,
            width: 190,
            render: (_, r) => (
              <Select
                size="small"
                value={r.componentType}
                options={TYPE_OPTIONS}
                style={{ width: 170 }}
                onChange={(v) => patch(setComponents, r.key, { componentType: v })}
              />
            ),
          },
          {
            title: text.pieces,
            width: 140,
            render: (_, r) => (
              <Space direction="vertical" size={2}>
                <InputNumber
                  size="small"
                  min={1}
                  max={99}
                  precision={0}
                  value={r.piecesPerGarment}
                  onChange={(v) => patch(setComponents, r.key, { piecesPerGarment: v ?? 1 })}
                />
                {/tay/i.test(r.componentName) && r.piecesPerGarment === 2 && (
                  <span style={{ fontSize: 12, opacity: 0.65 }}>Gồm tay trái + tay phải</span>
                )}
              </Space>
            ),
          },
          delCol(setComponents),
        ]}
      />
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        style={{ marginTop: 8 }}
        onClick={() =>
          setComponents((rows) => [
            ...rows,
            {
              key: newKey(),
              productComponentId: null,
              componentName: '',
              componentType: 'Panel',
              piecesPerGarment: 1,
            },
          ])
        }
      >
        {text.addComponent}
      </Button>
      {discontinued.length > 0 && (
        <Alert
          style={{ marginTop: 12 }}
          type="info"
          showIcon
          message={`${text.discontinued}: ${discontinued.join(', ')}`}
        />
      )}
    </>
  );

  const variantTab = (
    <>
      <Typography.Paragraph type="secondary">{text.variantHint}</Typography.Paragraph>
      <DataTable<VariantRow>
        rowKey="key"
        size="small"
        pagination={{ pageSize: 10, showSizeChanger: false, hideOnSinglePage: true }}
        dataSource={variants}
        columns={[
          { title: text.color, dataIndex: 'color' },
          { title: text.size, dataIndex: 'sizeCode', width: 120 },
          {
            title: text.variantLength,
            width: 190,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.lengthCm}
                style={{ width: 150 }}
                onChange={(v) => patch(setVariants, r.key, { lengthCm: v })}
              />
            ),
          },
          {
            title: text.variantWidth,
            width: 190,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.widthCm}
                style={{ width: 150 }}
                onChange={(v) => patch(setVariants, r.key, { widthCm: v })}
              />
            ),
          },
        ]}
      />
      {discontinuedVariants.length > 0 && (
        <Alert
          style={{ marginTop: 12 }}
          type="info"
          showIcon
          message={`${text.discontinuedVariants}: ${discontinuedVariants.join(', ')}`}
        />
      )}
    </>
  );

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

  const normTab = (
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
        columns={[
          {
            title: text.component,
            fixed: 'left' as const,
            render: (_, r) => (
              <Select
                size="small"
                value={r.componentName}
                options={componentOptions}
                style={{ width: 150 }}
                onChange={(v) => patch(setNorms, r.key, { componentName: v })}
              />
            ),
          },
          {
            title: text.material,
            render: (_, r) => (
              <Select
                size="small"
                showSearch
                optionFilterProp="label"
                value={r.materialId}
                options={materialOptions}
                loading={materials.isLoading}
                style={{ width: 260 }}
                onChange={(v) => patch(setNorms, r.key, { materialId: v })}
              />
            ),
          },
          {
            title: text.length,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.lengthCm}
                style={{ width: 90 }}
                onChange={(v) => patch(setNorms, r.key, { lengthCm: v })}
              />
            ),
          },
          {
            title: text.width,
            render: (_, r) => (
              <InputNumber
                size="small"
                min={0}
                precision={2}
                value={r.widthCm}
                style={{ width: 90 }}
                onChange={(v) => patch(setNorms, r.key, { widthCm: v })}
              />
            ),
          },
          {
            title: text.appliesTo,
            render: (_, r) => (
              <Select
                size="small"
                mode="multiple"
                maxTagCount="responsive"
                value={r.variantKeys}
                options={variantOptions}
                style={{ minWidth: 220 }}
                onChange={(v) => patch(setNorms, r.key, { variantKeys: v })}
                dropdownRender={(menu) => (
                  <>
                    {menu}
                    <Button
                      type="link"
                      size="small"
                      onClick={() =>
                        patch(setNorms, r.key, { variantKeys: variantOptions.map((o) => o.value) })
                      }
                    >
                      {text.allVariants}
                    </Button>
                  </>
                )}
              />
            ),
          },
          {
            title: text.note,
            render: (_, r) => (
              <Input
                size="small"
                value={r.specNote}
                maxLength={200}
                style={{ width: 160 }}
                onChange={(e) => patch(setNorms, r.key, { specNote: e.target.value })}
              />
            ),
          },
          delCol(setNorms),
        ]}
      />
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        style={{ marginTop: 8 }}
        onClick={() => {
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
        }}
      >
        {text.addNorm}
      </Button>
    </>
  );

  return (
    <>
      <ModalActions>
        <Button onClick={onClose}>{MESSAGES.common.close}</Button>
        <Button type="primary" loading={save.isPending} onClick={handleSave}>
          {MESSAGES.common.save}
        </Button>
      </ModalActions>
      {detail?.description && (
        <Typography.Paragraph type="secondary">{`${MESSAGES.orders.description}: ${detail.description}`}</Typography.Paragraph>
      )}
      {error && <Alert type="warning" showIcon message={error} style={{ marginBottom: 12 }} />}
      <Tabs
        items={[
          {
            key: 'components',
            label: `${text.tabComponents} (${components.length})`,
            children: componentTab,
          },
          {
            key: 'variants',
            label: `${text.tabVariants} (${variants.length})`,
            children: variantTab,
          },
          { key: 'norms', label: `${text.tabNorms} (${norms.length})`, children: normTab },
          ...(showHistory && detail
            ? [
                {
                  key: 'history',
                  label: MESSAGES.common.history,
                  children: <AuditHistory history={detail.history} />,
                },
              ]
            : []),
        ]}
      />
    </>
  );
}
