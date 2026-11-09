import type { Dispatch, SetStateAction } from 'react';
import { Input, InputNumber, Select, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { OrderMarkerStatus } from '../types';
import {
  ORDER_MARKER_LIMITS,
  piecesOf,
  withPieces,
  type OrderMarkerRow,
} from '../utils/orderMarker';
import { dropRow, patchRow } from '../utils/productSpec';
import { deleteRowColumn } from './deleteRowColumn';

const text = MESSAGES.orders.markers;

const STATUS_OPTIONS: { value: OrderMarkerStatus; label: string }[] = [
  { value: 'Active', label: text.active },
  { value: 'Discontinued', label: text.discontinued },
];

type OrderMarkerColumnsOptions = {
  sizes: string[];
  groups: number[];
  readOnly: boolean;
  setRows: Dispatch<SetStateAction<OrderMarkerRow[]>>;
};

/** Cột bảng bìa của một mã hàng: nhóm, mã, dài, số sản phẩm theo từng size của đơn, trạng thái. */
export const orderMarkerColumns = ({
  sizes,
  groups,
  readOnly,
  setRows,
}: OrderMarkerColumnsOptions): TableColumnsType<OrderMarkerRow> => {
  const patch = (key: string, change: Partial<OrderMarkerRow>) => patchRow(setRows, key, change);
  return [
    {
      title: text.group,
      fixed: 'left' as const,
      render: (_, r) => (
        <Select
          size="small"
          disabled={readOnly}
          aria-label={text.group}
          value={r.markerNo}
          options={groups.map((g) => ({ value: g, label: fill(text.groupLabel, g) }))}
          style={{ width: 100 }}
          onChange={(markerNo: number) => patch(r.key, { markerNo })}
        />
      ),
    },
    {
      title: text.code,
      render: (_, r) => (
        <Input
          size="small"
          disabled={readOnly}
          aria-label={text.code}
          maxLength={ORDER_MARKER_LIMITS.maxCodeLength}
          value={r.markerCode}
          style={{ width: 100 }}
          onChange={(e) => patch(r.key, { markerCode: e.target.value })}
        />
      ),
    },
    {
      title: text.lengthM,
      render: (_, r) => (
        <InputNumber
          size="small"
          disabled={readOnly}
          aria-label={text.lengthM}
          min={0.01}
          max={ORDER_MARKER_LIMITS.maxLengthM}
          precision={2}
          value={r.lengthM}
          style={{ width: 90 }}
          onChange={(lengthM) => patch(r.key, { lengthM })}
        />
      ),
    },
    ...sizes.map((size) => ({
      title: size,
      key: `size-${size}`,
      render: (_: unknown, r: OrderMarkerRow) => (
        <InputNumber
          size="small"
          disabled={readOnly}
          aria-label={`${text.code} ${r.markerCode} ${size}`}
          min={1}
          max={ORDER_MARKER_LIMITS.maxPiecesPerPly}
          precision={0}
          value={piecesOf(r, size)}
          style={{ width: 64 }}
          onChange={(value) => patch(r.key, withPieces(r, size, value))}
        />
      ),
    })),
    {
      title: text.status,
      render: (_, r) => (
        <Select
          size="small"
          disabled={readOnly}
          aria-label={text.status}
          value={r.status}
          options={STATUS_OPTIONS}
          style={{ width: 120 }}
          onChange={(status: OrderMarkerStatus) => patch(r.key, { status })}
        />
      ),
    },
    ...(readOnly ? [] : [deleteRowColumn<OrderMarkerRow>((key) => dropRow(setRows, key))]),
  ];
};
