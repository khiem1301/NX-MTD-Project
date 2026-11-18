import { Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import type {
  SpecImportComponentRow,
  SpecImportMarkerGroupRow,
  SpecImportNormRow,
  SpecImportOrderMarkerRow,
} from '../types';

const text = MESSAGES.orders.specImport;
const col = text.columns;
const EMPTY = '—';

type Row = { rowNumber: number; action: string; error: string | null };

/** Hai cột chung: dòng Excel (đầu bảng) và kết quả + lý do lỗi (cuối bảng). */
const rowColumn = { title: col.row, dataIndex: 'rowNumber', width: 64 };
const resultColumn = {
  title: col.result,
  key: 'result',
  width: 260,
  render: (_: unknown, row: Row) => (
    <Space direction="vertical" size={0}>
      <StatusTag status={row.action} labels={text.action} />
      {row.error && <Typography.Text type="danger">{row.error}</Typography.Text>}
    </Space>
  ),
};

const list = (values: string[]) => (values.length === 0 ? EMPTY : values.join(', '));
const num = (v: number | null) => (v === null ? EMPTY : formatQty(v));

export const componentImportColumns: ColumnsType<SpecImportComponentRow> = [
  rowColumn,
  { title: col.name, dataIndex: 'componentName' },
  {
    title: col.type,
    dataIndex: 'componentType',
    width: 90,
    render: (v: string | null) => v ?? EMPTY,
  },
  { title: col.pieces, dataIndex: 'piecesPerGarment', width: 120, render: num },
  resultColumn,
];

export const normImportColumns: ColumnsType<SpecImportNormRow> = [
  rowColumn,
  { title: col.color, dataIndex: 'color', width: 110 },
  { title: col.component, dataIndex: 'componentName' },
  { title: col.materialCode, dataIndex: 'materialCode', width: 130 },
  {
    title: col.material,
    key: 'material',
    render: (_, r) =>
      r.materialName ? `${r.materialName}${r.materialColor ? ` (${r.materialColor})` : ''}` : EMPTY,
  },
  { title: col.note, dataIndex: 'specNote', render: (v: string | null) => v ?? EMPTY },
  resultColumn,
];

export const groupImportColumns: ColumnsType<SpecImportMarkerGroupRow> = [
  rowColumn,
  { title: col.markerNo, dataIndex: 'markerNo', width: 70, render: num },
  { title: col.components, dataIndex: 'componentNames', render: list },
  { title: col.colors, dataIndex: 'colors', render: list },
  resultColumn,
];

export const markerImportColumns: ColumnsType<SpecImportOrderMarkerRow> = [
  rowColumn,
  { title: col.markerNo, dataIndex: 'markerNo', width: 70, render: num },
  { title: col.markerCode, dataIndex: 'markerCode', width: 90 },
  { title: col.lengthM, dataIndex: 'lengthM', width: 90, align: 'right', render: num },
  { title: col.widthM, dataIndex: 'widthM', width: 90, align: 'right', render: num },
  {
    title: col.status,
    dataIndex: 'status',
    width: 110,
    render: (v: string) =>
      v === 'Discontinued' ? MESSAGES.orders.markers.discontinued : MESSAGES.orders.markers.active,
  },
  {
    title: col.ratios,
    dataIndex: 'ratios',
    render: (ratios: SpecImportOrderMarkerRow['ratios']) =>
      list(ratios.map((r) => `${r.sizeCode}: ${r.piecesPerPly}`)),
  },
  resultColumn,
];
