import { useMemo, useState } from 'react';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Drawer, Input, InputNumber, Select, Space, Spin, Table, Tabs, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { useProductSpec, useSaveProductSpec } from '../hooks/useOrders';
import type { ProductListItem, ProductSpec } from '../types';
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

type Props = { product: ProductListItem | null; onClose: () => void };

const text = MESSAGES.orders.spec;
const TYPE_OPTIONS = [
  { value: 'Panel', label: text.typePanel },
  { value: 'Trim', label: text.typeTrim },
];

/**
 * UC10 (định mức): khai báo một lần cho mã hàng — thành phần (chi tiết cắt / phụ liệu), các biến thể màu × size và NVL của
 * từng thành phần. Một dòng định mức áp dụng cho nhiều màu – size cùng lúc; máy chủ lưu theo từng biến thể.
 */
export function ProductSpecDrawer({ product, onClose }: Props) {
  const spec = useProductSpec(product?.productId);
  return (
    <Drawer
      open={product !== null}
      width={1180}
      title={product ? `${text.title}: ${product.productCode} - ${product.productName}` : text.title}
      onClose={onClose}
      destroyOnHidden
    >
      {spec.error && <Alert type="error" showIcon message={spec.error.message} style={{ marginBottom: 12 }} />}
      {spec.isLoading && <Spin />}
      {product && spec.data && (
        <SpecEditor key={spec.dataUpdatedAt} product={product} spec={spec.data} onClose={onClose} />
      )}
    </Drawer>
  );
}

/** Phần soạn: khởi tạo từ dữ liệu máy chủ; lưu xong dữ liệu tải lại (đổi key) nên dựng lại từ bản đã lưu. */
function SpecEditor({ product, spec, onClose }: { product: ProductListItem; spec: ProductSpec; onClose: () => void }) {
  const save = useSaveProductSpec();
  const materials = useMaterialOptions();
  const [initial] = useState(() => toRows(spec));
  const [components, setComponents] = useState<ComponentRow[]>(initial.components);
  const [variants, setVariants] = useState<VariantRow[]>(initial.variants);
  const [norms, setNorms] = useState<NormRow[]>(initial.norms);
  const [error, setError] = useState('');

  const patch = <T extends { key: string }>(set: (f: (rows: T[]) => T[]) => void, key: string, change: Partial<T>) =>
    set((rows) => rows.map((r) => (r.key === key ? { ...r, ...change } : r)));

  const variantOptions = useMemo(
    () => variants.map((v) => ({ value: variantKey(v.color, v.sizeCode), label: `${v.color || '?'} - ${v.sizeCode || '?'}` })),
    [variants],
  );
  const componentOptions = components
    .filter((c) => c.componentName.trim())
    .map((c) => ({ value: c.componentName.trim(), label: c.componentName.trim() }));
  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} - ${m.materialName} (${m.unit})`,
  }));

  const handleSave = () => {
    const problem = validate(components, variants, norms);
    setError(problem);
    if (problem) return;
    save.mutate({ id: product.productId, body: toPayload(components, variants, norms) });
  };

  const discontinued = spec.components.filter((c) => c.status !== 'Active').map((c) => c.componentName);
  const discontinuedVariants = spec.variants.filter((v) => v.status !== 'Active').map((v) => `${v.color}-${v.sizeCode}`);

  const delCol = <T extends { key: string }>(set: (f: (rows: T[]) => T[]) => void) => ({
    title: '',
    width: 48,
    render: (_: unknown, r: T) => (
      <Button size="small" danger icon={<DeleteOutlined />} onClick={() => set((rows) => rows.filter((x) => x.key !== r.key))} />
    ),
  });

  const componentTab = (
    <>
      <Typography.Paragraph type="secondary">{text.componentHint}</Typography.Paragraph>
      <Table<ComponentRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={components}
        columns={[
          {
            title: text.componentName,
            render: (_, r) => (
              <Input value={r.componentName} maxLength={100} onChange={(e) => patch(setComponents, r.key, { componentName: e.target.value })} />
            ),
          },
          {
            title: text.componentType,
            width: 190,
            render: (_, r) => (
              <Select value={r.componentType} options={TYPE_OPTIONS} style={{ width: 170 }} onChange={(v) => patch(setComponents, r.key, { componentType: v })} />
            ),
          },
          {
            title: text.pieces,
            width: 140,
            render: (_, r) => (
              <InputNumber min={1} max={99} precision={0} value={r.piecesPerGarment} onChange={(v) => patch(setComponents, r.key, { piecesPerGarment: v ?? 1 })} />
            ),
          },
          {
            title: text.sortOrder,
            width: 110,
            render: (_, r) => (
              <InputNumber min={0} max={99} precision={0} value={r.sortOrder} onChange={(v) => patch(setComponents, r.key, { sortOrder: v ?? 0 })} />
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
            { key: newKey(), productComponentId: null, componentName: '', componentType: 'Panel', piecesPerGarment: 1, sortOrder: rows.length + 1 },
          ])
        }
      >
        {text.addComponent}
      </Button>
      {discontinued.length > 0 && <Alert style={{ marginTop: 12 }} type="info" showIcon message={`${text.discontinued}: ${discontinued.join(', ')}`} />}
    </>
  );

  const variantTab = (
    <>
      <Typography.Paragraph type="secondary">{text.variantHint}</Typography.Paragraph>
      <Table<VariantRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={variants}
        columns={[
          {
            title: text.color,
            render: (_, r) => <Input value={r.color} maxLength={50} onChange={(e) => patch(setVariants, r.key, { color: e.target.value })} />,
          },
          {
            title: text.size,
            width: 160,
            render: (_, r) => (
              <Input value={r.sizeCode} maxLength={10} onChange={(e) => patch(setVariants, r.key, { sizeCode: e.target.value.toUpperCase() })} />
            ),
          },
          delCol(setVariants),
        ]}
      />
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        style={{ marginTop: 8 }}
        onClick={() => setVariants((rows) => [...rows, { key: newKey(), productVariantId: null, color: rows.at(-1)?.color ?? '', sizeCode: '' }])}
      >
        {text.addVariant}
      </Button>
      {discontinuedVariants.length > 0 && (
        <Alert style={{ marginTop: 12 }} type="info" showIcon message={`${text.discontinuedVariants}: ${discontinuedVariants.join(', ')}`} />
      )}
    </>
  );

  const normTab = (
    <>
      <Typography.Paragraph type="secondary">{text.normHint}</Typography.Paragraph>
      <Table<NormRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={norms}
        scroll={{ x: 'max-content' }}
        columns={[
          {
            title: text.component,
            render: (_, r) => (
              <Select value={r.componentName} options={componentOptions} style={{ width: 150 }} onChange={(v) => patch(setNorms, r.key, { componentName: v })} />
            ),
          },
          {
            title: text.material,
            render: (_, r) => (
              <Select
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
            title: text.materialColor,
            render: (_, r) => (
              <Input value={r.materialColor} maxLength={50} style={{ width: 110 }} onChange={(e) => patch(setNorms, r.key, { materialColor: e.target.value })} />
            ),
          },
          {
            title: text.qty,
            render: (_, r) => (
              <InputNumber min={0} precision={4} value={r.qtyPerGarment} style={{ width: 110 }} onChange={(v) => patch(setNorms, r.key, { qtyPerGarment: v })} />
            ),
          },
          {
            title: text.wastage,
            render: (_, r) => (
              <InputNumber min={0} max={100} precision={2} value={r.wastagePercent} style={{ width: 90 }} onChange={(v) => patch(setNorms, r.key, { wastagePercent: v ?? 0 })} />
            ),
          },
          {
            title: text.length,
            render: (_, r) => (
              <InputNumber min={0} precision={2} value={r.lengthCm} style={{ width: 90 }} onChange={(v) => patch(setNorms, r.key, { lengthCm: v })} />
            ),
          },
          {
            title: text.width,
            render: (_, r) => (
              <InputNumber min={0} precision={2} value={r.widthCm} style={{ width: 90 }} onChange={(v) => patch(setNorms, r.key, { widthCm: v })} />
            ),
          },
          {
            title: text.appliesTo,
            render: (_, r) => (
              <Select
                mode="multiple"
                maxTagCount="responsive"
                value={r.variantKeys}
                options={variantOptions}
                style={{ minWidth: 220 }}
                onChange={(v) => patch(setNorms, r.key, { variantKeys: v })}
                dropdownRender={(menu) => (
                  <>
                    {menu}
                    <Button type="link" size="small" onClick={() => patch(setNorms, r.key, { variantKeys: variantOptions.map((o) => o.value) })}>
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
              <Input value={r.specNote} maxLength={200} style={{ width: 160 }} onChange={(e) => patch(setNorms, r.key, { specNote: e.target.value })} />
            ),
          },
          delCol(setNorms),
        ]}
      />
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        style={{ marginTop: 8 }}
        onClick={() =>
          setNorms((rows) => [
            ...rows,
            {
              key: newKey(),
              componentName: undefined,
              materialId: undefined,
              materialColor: '',
              qtyPerGarment: null,
              wastagePercent: 0,
              lengthCm: null,
              widthCm: null,
              specNote: '',
              variantKeys: variantOptions.map((o) => o.value),
            },
          ])
        }
      >
        {text.addNorm}
      </Button>
    </>
  );

  return (
    <>
      <Space style={{ marginBottom: 12 }}>
        <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
        <Button type="primary" loading={save.isPending} onClick={handleSave}>
          {MESSAGES.common.save}
        </Button>
      </Space>
      {error && <Alert type="warning" showIcon message={error} style={{ marginBottom: 12 }} />}
      <Tabs
        items={[
          { key: 'components', label: `${text.tabComponents} (${components.length})`, children: componentTab },
          { key: 'variants', label: `${text.tabVariants} (${variants.length})`, children: variantTab },
          { key: 'norms', label: `${text.tabNorms} (${norms.length})`, children: normTab },
        ]}
      />
    </>
  );
}
