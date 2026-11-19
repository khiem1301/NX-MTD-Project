import { AutoComplete, Button, Checkbox, Input, InputNumber, Select } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { EXCEL_COMPONENT_TYPE } from '../constants';
import type { RowStatus } from '../utils/excelReview';
import { ExcelReviewStatusCell } from './ExcelReviewStatusCell';
import type {
  OrderMaterialExcelRow,
  ProductExcelRow,
  SpecComponentExcelRow,
  SpecMarkerGroupExcelRow,
  SpecNormExcelRow,
  SpecOrderMarkerExcelRow,
} from '../types';

const text = MESSAGES.orders.excelReview;
const col = text.columns;

/** DEV-ALL-122: thao tác của một bảng trong popup kiểm tra Excel (sửa / xóa dòng, kết quả kiểm theo rowNumber). */
export type ReviewColumnsCtx<R> = {
  update: (rowNumber: number, patch: Partial<R>) => void;
  remove: (rowNumber: number) => void;
  status: Map<number, RowStatus>;
  creating?: boolean;
};

const rowCol = { title: col.row, dataIndex: 'rowNumber', width: 64 };
const RESULT_WIDTH = 240;
const MARKER_STATUS = { ACTIVE: 'ACTIVE', DISCONTINUED: 'DISCONTINUED' } as const;

const tail = <R extends { rowNumber: number; parseError?: string | null }>(
  ctx: ReviewColumnsCtx<R>,
) => [
  {
    title: col.result,
    key: 'result',
    width: RESULT_WIDTH,
    render: (_: unknown, r: R) => (
      <ExcelReviewStatusCell
        status={ctx.status.get(r.rowNumber)}
        parseError={r.parseError}
        creating={ctx.creating}
      />
    ),
  },
  {
    key: 'remove',
    width: 48,
    // Ghim phải để nút xóa không bị thanh cuộn ngang che.
    fixed: 'right' as const,
    render: (_: unknown, r: R) => (
      <Button
        type="text"
        danger
        size="small"
        aria-label={MESSAGES.common.delete}
        icon={<DeleteOutlined />}
        onClick={() => ctx.remove(r.rowNumber)}
      />
    ),
  },
];

const text$ = (v: string | null | undefined) => v ?? '';
const opts = (values: string[]) =>
  [...new Set(values.filter((v) => v.trim()))].map((v) => ({ value: v, label: v }));

export const productColumns = (
  ctx: ReviewColumnsCtx<ProductExcelRow>,
): ColumnsType<ProductExcelRow> => [
  rowCol,
  {
    title: col.productCode,
    key: 'code',
    width: 180,
    render: (_, r) => (
      <Input
        value={r.productCode}
        onChange={(e) => ctx.update(r.rowNumber, { productCode: e.target.value.toUpperCase() })}
      />
    ),
  },
  {
    title: col.productName,
    key: 'name',
    render: (_, r) => (
      <Input
        value={r.productName}
        onChange={(e) => ctx.update(r.rowNumber, { productName: e.target.value })}
      />
    ),
  },
  {
    title: col.description,
    key: 'desc',
    render: (_, r) => (
      <Input
        value={text$(r.description)}
        onChange={(e) => ctx.update(r.rowNumber, { description: e.target.value || null })}
      />
    ),
  },
  ...tail(ctx),
];

export const componentColumns = (
  ctx: ReviewColumnsCtx<SpecComponentExcelRow>,
): ColumnsType<SpecComponentExcelRow> => [
  rowCol,
  {
    title: col.name,
    key: 'name',
    render: (_, r) => (
      <Input value={r.name} onChange={(e) => ctx.update(r.rowNumber, { name: e.target.value })} />
    ),
  },
  {
    title: col.type,
    key: 'type',
    width: 130,
    render: (_, r) => (
      <Select
        style={{ width: '100%' }}
        value={r.componentType ?? undefined}
        options={[
          { value: EXCEL_COMPONENT_TYPE.PANEL, label: text.componentTypes.PANEL },
          { value: EXCEL_COMPONENT_TYPE.TRIM, label: text.componentTypes.TRIM },
        ]}
        onChange={(componentType) => ctx.update(r.rowNumber, { componentType })}
      />
    ),
  },
  {
    title: col.pieces,
    key: 'pieces',
    width: 120,
    render: (_, r) => (
      <InputNumber
        min={1}
        max={99}
        precision={0}
        value={r.piecesPerGarment}
        onChange={(v) => ctx.update(r.rowNumber, { piecesPerGarment: v })}
      />
    ),
  },
  ...tail(ctx),
];

export const normColumns = (
  ctx: ReviewColumnsCtx<SpecNormExcelRow>,
  componentNames: string[],
  colors: string[],
  materialOptions: { value: string; label: string }[],
): ColumnsType<SpecNormExcelRow> => [
  rowCol,
  {
    title: col.color,
    key: 'color',
    width: 120,
    render: (_, r) => (
      <AutoComplete
        style={{ width: '100%' }}
        value={r.color}
        options={opts(colors)}
        onChange={(color: string) => ctx.update(r.rowNumber, { color })}
      />
    ),
  },
  {
    title: col.component,
    key: 'component',
    width: 150,
    render: (_, r) => (
      <AutoComplete
        style={{ width: '100%' }}
        value={r.componentName}
        options={opts(componentNames)}
        onChange={(componentName: string) => ctx.update(r.rowNumber, { componentName })}
      />
    ),
  },
  {
    title: col.material,
    key: 'material',
    width: 220,
    render: (_, r) => (
      <Select
        showSearch
        style={{ width: '100%' }}
        value={r.materialCode || undefined}
        options={materialOptions}
        optionFilterProp="label"
        onChange={(materialCode: string) => ctx.update(r.rowNumber, { materialCode })}
      />
    ),
  },
  {
    title: col.specNote,
    key: 'note',
    render: (_, r) => (
      <Input
        value={text$(r.specNote)}
        onChange={(e) => ctx.update(r.rowNumber, { specNote: e.target.value || null })}
      />
    ),
  },
  ...tail(ctx),
];

export const groupColumns = (
  ctx: ReviewColumnsCtx<SpecMarkerGroupExcelRow>,
  panelNames: string[],
  colors: string[],
): ColumnsType<SpecMarkerGroupExcelRow> => [
  rowCol,
  {
    title: col.markerNo,
    key: 'no',
    width: 100,
    render: (_, r) => (
      <InputNumber
        min={1}
        precision={0}
        value={r.markerNo}
        onChange={(v) => ctx.update(r.rowNumber, { markerNo: v })}
      />
    ),
  },
  {
    title: col.groupComponents,
    key: 'components',
    render: (_, r) => (
      <Select
        mode="tags"
        style={{ width: '100%' }}
        value={r.componentNames}
        options={opts(panelNames)}
        onChange={(componentNames: string[]) => ctx.update(r.rowNumber, { componentNames })}
      />
    ),
  },
  {
    title: col.groupColors,
    key: 'colors',
    width: 240,
    render: (_, r) => (
      <Select
        mode="tags"
        style={{ width: '100%' }}
        value={r.colors}
        options={opts(colors)}
        onChange={(next: string[]) => ctx.update(r.rowNumber, { colors: next })}
      />
    ),
  },
  ...tail(ctx),
];

export const markerColumns = (
  ctx: ReviewColumnsCtx<SpecOrderMarkerExcelRow>,
  sizes: string[],
): ColumnsType<SpecOrderMarkerExcelRow> => [
  rowCol,
  {
    title: col.markerNo,
    key: 'no',
    width: 90,
    render: (_, r) => (
      <InputNumber
        min={1}
        precision={0}
        value={r.markerNo}
        onChange={(v) => ctx.update(r.rowNumber, { markerNo: v })}
      />
    ),
  },
  {
    title: col.markerCode,
    key: 'code',
    width: 100,
    render: (_, r) => (
      <Input
        value={r.markerCode}
        onChange={(e) => ctx.update(r.rowNumber, { markerCode: e.target.value.toUpperCase() })}
      />
    ),
  },
  {
    title: col.length,
    key: 'length',
    width: 100,
    render: (_, r) => (
      <InputNumber
        min={0}
        step={0.1}
        value={r.lengthM}
        onChange={(v) => ctx.update(r.rowNumber, { lengthM: v })}
      />
    ),
  },
  {
    title: col.width,
    key: 'width',
    width: 100,
    render: (_, r) => (
      <InputNumber
        min={0}
        step={0.1}
        value={r.widthM}
        onChange={(v) => ctx.update(r.rowNumber, { widthM: v })}
      />
    ),
  },
  {
    title: col.markerStatus,
    key: 'status',
    width: 110,
    align: 'center',
    render: (_, r) => (
      <Checkbox
        checked={r.status === MARKER_STATUS.DISCONTINUED}
        onChange={(e) =>
          ctx.update(r.rowNumber, {
            status: e.target.checked ? MARKER_STATUS.DISCONTINUED : MARKER_STATUS.ACTIVE,
          })
        }
      />
    ),
  },
  ...sizes.map((size) => ({
    title: size,
    key: `size-${size}`,
    width: 76,
    render: (_: unknown, r: SpecOrderMarkerExcelRow) => (
      <InputNumber
        min={0}
        precision={0}
        style={{ width: 64 }}
        value={r.ratios.find((x) => x.sizeCode === size)?.piecesPerPly ?? null}
        onChange={(v) =>
          ctx.update(r.rowNumber, {
            ratios: [
              ...r.ratios.filter((x) => x.sizeCode !== size),
              ...(v ? [{ sizeCode: size, piecesPerPly: v }] : []),
            ],
          })
        }
      />
    ),
  })),
  ...tail(ctx),
];

export const orderMaterialColumns = (
  ctx: ReviewColumnsCtx<OrderMaterialExcelRow>,
  materialOptions: { value: string; label: string }[],
): ColumnsType<OrderMaterialExcelRow> => [
  rowCol,
  {
    title: col.material,
    key: 'material',
    width: 240,
    render: (_, r) => (
      <Select
        showSearch
        style={{ width: '100%' }}
        value={r.materialCode || undefined}
        options={materialOptions}
        optionFilterProp="label"
        onChange={(materialCode: string) => ctx.update(r.rowNumber, { materialCode })}
      />
    ),
  },
  {
    // DEV-ALL-123: tên NVL theo file (NVL đã có thì danh mục quyết định; NVL chưa có thì làm tên gợi ý khi thêm vào danh mục).
    title: col.materialName,
    key: 'name',
    width: 180,
    render: (_, r) => (
      <Input
        value={text$(r.materialName)}
        onChange={(e) => ctx.update(r.rowNumber, { materialName: e.target.value || null })}
      />
    ),
  },
  {
    title: col.unit,
    key: 'unit',
    width: 90,
    render: (_, r) => (
      <Input
        value={text$(r.unit)}
        onChange={(e) => ctx.update(r.rowNumber, { unit: e.target.value || null })}
      />
    ),
  },
  {
    title: col.requiredQty,
    key: 'required',
    width: 120,
    render: (_, r) => (
      <InputNumber
        min={0}
        value={r.requiredQty}
        onChange={(v) => ctx.update(r.rowNumber, { requiredQty: v })}
      />
    ),
  },
  {
    title: col.customerQty,
    key: 'customer',
    width: 120,
    render: (_, r) => (
      <InputNumber
        min={0}
        value={r.customerQty}
        onChange={(v) => ctx.update(r.rowNumber, { customerQty: v })}
      />
    ),
  },
  {
    title: col.note,
    key: 'note',
    render: (_, r) => (
      <Input
        value={text$(r.note)}
        onChange={(e) => ctx.update(r.rowNumber, { note: e.target.value || null })}
      />
    ),
  },
  ...tail(ctx),
];
