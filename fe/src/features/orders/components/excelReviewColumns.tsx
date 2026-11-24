import { cloneElement, type ReactElement } from 'react';
import { AutoComplete, Button, Input, InputNumber, Select, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, WarningOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { EXCEL_COMPONENT_TYPE, EXCEL_ROW_ACTION } from '../constants';
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
  /** Bảng có mã NVL (Định mức, NVL của đơn): mã NVL của dòng và thao tác mở lại NVL ngừng sử dụng (chỉ Admin). */
  materialCodeOf?: (r: R) => string;
  reopenMaterial?: (code: string) => void;
  reopeningCode?: string;
};

/**
 * DEV-ALL-154: chỉ lỗi thật (định dạng / Action = Error). Ghi chú Pending ("Kiểm khi Lưu đơn") không tô cam ô nhập —
 * hiện ở cột Kết quả.
 */
const rowError = <R extends { rowNumber: number; parseError?: string | null }>(
  ctx: ReviewColumnsCtx<R>,
  r: R,
) => {
  if (r.parseError) return r.parseError;
  const status = ctx.status.get(r.rowNumber);
  return status?.action === EXCEL_ROW_ACTION.ERROR ? status.error : null;
};

/**
 * Giữ nguyên cấu trúc ô dù có lỗi hay không (luôn bọc Tooltip, luôn có suffix): đổi cấu trúc làm React dựng lại ô nhập,
 * mất con trỏ sau mỗi ký tự gõ.
 */
const withWarn = (error: string | null, node: ReactElement) => (
  <Tooltip title={error ?? undefined} color="orange">
    <span style={{ display: 'block' }}>
      {cloneElement(node, {
        status: error ? ('warning' as const) : undefined,
        suffix: error ? <WarningOutlined /> : <span />,
      })}
    </span>
  </Tooltip>
);

const rowCol = { title: col.row, dataIndex: 'rowNumber', width: 64 };
const RESULT_WIDTH = 200;
const MARKER_STATUS = { ACTIVE: 'ACTIVE', DISCONTINUED: 'DISCONTINUED' } as const;

const tail = <R extends { rowNumber: number; parseError?: string | null }>(
  ctx: ReviewColumnsCtx<R>,
) => [
  {
    title: col.result,
    key: 'result',
    width: RESULT_WIDTH,
    // Ghim phải cạnh nút xóa: không bị cuộn ngang che (file nhiều cột size / NVL).
    fixed: 'right' as const,
    render: (_: unknown, r: R) => (
      <ExcelReviewStatusCell
        status={ctx.status.get(r.rowNumber)}
        parseError={r.parseError}
        creating={ctx.creating}
        onReopen={
          ctx.reopenMaterial && ctx.materialCodeOf
            ? () => ctx.reopenMaterial?.(ctx.materialCodeOf?.(r) ?? '')
            : undefined
        }
        reopening={ctx.reopeningCode !== undefined && ctx.reopeningCode === ctx.materialCodeOf?.(r)}
      />
    ),
  },
  {
    key: 'remove',
    width: 48,
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
    title: col.productCode,
    key: 'product',
    width: 120,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <Input
          value={r.productCode ?? ''}
          onChange={(e) =>
            ctx.update(r.rowNumber, { productCode: e.target.value.toUpperCase() || null })
          }
        />,
      ),
  },
  {
    title: col.markerNo,
    key: 'no',
    width: 90,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <InputNumber
          min={1}
          precision={0}
          value={r.markerNo}
          onChange={(v) => ctx.update(r.rowNumber, { markerNo: v })}
        />,
      ),
  },
  {
    title: col.markerCode,
    key: 'code',
    width: 100,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <Input
          value={r.markerCode}
          onChange={(e) => ctx.update(r.rowNumber, { markerCode: e.target.value.toUpperCase() })}
        />,
      ),
  },
  {
    title: col.length,
    key: 'length',
    width: 100,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <InputNumber
          min={0}
          step={0.1}
          value={r.lengthM}
          onChange={(v) => ctx.update(r.rowNumber, { lengthM: v })}
        />,
      ),
  },
  {
    title: col.width,
    key: 'width',
    width: 100,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <InputNumber
          min={0}
          step={0.1}
          value={r.widthM}
          onChange={(v) => ctx.update(r.rowNumber, { widthM: v })}
        />,
      ),
  },
  {
    // Khớp cột Trạng thái của file mẫu bìa (Đang dùng / Ngừng dùng).
    title: col.markerStatus,
    key: 'status',
    width: 130,
    render: (_, r) => (
      <Select
        style={{ width: '100%' }}
        value={
          r.status === MARKER_STATUS.DISCONTINUED
            ? MARKER_STATUS.DISCONTINUED
            : MARKER_STATUS.ACTIVE
        }
        options={[
          { value: MARKER_STATUS.ACTIVE, label: MESSAGES.orders.markers.active },
          { value: MARKER_STATUS.DISCONTINUED, label: MESSAGES.orders.markers.discontinued },
        ]}
        onChange={(status: string) => ctx.update(r.rowNumber, { status })}
      />
    ),
  },
  ...sizes.map((size) => ({
    title: size,
    key: `size-${size}`,
    width: 76,
    render: (_: unknown, r: SpecOrderMarkerExcelRow) =>
      withWarn(
        rowError(ctx, r),
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
        />,
      ),
  })),
  ...tail(ctx),
];

/** Khớp file mẫu Mau_NVL_Cua_Don: Mã NVL, Số cần, Khách cấp, Ghi chú (Tên / Đơn vị chỉ dùng khi thêm NVL mới ở panel riêng). */
export const orderMaterialColumns = (
  ctx: ReviewColumnsCtx<OrderMaterialExcelRow>,
  materialOptions: { value: string; label: string }[],
): ColumnsType<OrderMaterialExcelRow> => [
  rowCol,
  {
    title: col.material,
    key: 'material',
    width: 260,
    render: (_, r) => {
      const error = rowError(ctx, r);
      const field = (
        <Select
          showSearch
          style={{ width: '100%' }}
          status={error ? 'warning' : undefined}
          value={r.materialCode || undefined}
          options={materialOptions}
          optionFilterProp="label"
          onChange={(materialCode: string) => ctx.update(r.rowNumber, { materialCode })}
        />
      );
      return error ? (
        <Tooltip title={error} color="orange">
          <span style={{ display: 'block' }}>{field}</span>
        </Tooltip>
      ) : (
        field
      );
    },
  },
  {
    title: col.requiredQty,
    key: 'required',
    width: 120,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <InputNumber
          min={0}
          value={r.requiredQty}
          onChange={(v) => ctx.update(r.rowNumber, { requiredQty: v })}
        />,
      ),
  },
  {
    title: col.customerQty,
    key: 'customer',
    width: 120,
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <InputNumber
          min={0}
          value={r.customerQty}
          onChange={(v) => ctx.update(r.rowNumber, { customerQty: v })}
        />,
      ),
  },
  {
    title: col.note,
    key: 'note',
    render: (_, r) =>
      withWarn(
        rowError(ctx, r),
        <Input
          value={text$(r.note)}
          onChange={(e) => ctx.update(r.rowNumber, { note: e.target.value || null })}
        />,
      ),
  },
  ...tail(ctx),
];
