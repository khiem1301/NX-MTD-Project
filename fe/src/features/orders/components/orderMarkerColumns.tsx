import type { Dispatch, SetStateAction } from 'react';
import { WarningOutlined } from '@ant-design/icons';
import { Input, Select, Tooltip, type TableColumnsType } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { WIDTH_DECIMALS, WIDTH_MAX_M, WIDTH_MIN_M } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { OrderMarkerGroup, OrderMarkerStatus } from '../types';
import {
  ORDER_MARKER_LIMITS,
  piecesOf,
  tooWideWarning,
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

/**
 * Bề rộng cột (px). Cột có bề rộng cố định; bảng có bề rộng tối thiểu tính sẵn và cuộn bằng khung ngoài, để antd không phải
 * đo nội dung ô nhập mỗi lần vẽ (đo theo nội dung làm thanh cuộn hiện / mất liên tục, bảng giật hình).
 */
const WIDTH = {
  group: 180,
  code: 120,
  length: 110,
  width: 120,
  size: 76,
  status: 130,
  remove: 48,
} as const;
const FULL = { width: '100%' } as const;

/** Bề rộng tối thiểu của bảng (tổng bề rộng cột). */
export const orderMarkerTableWidth = (sizeCount: number, readOnly: boolean) =>
  WIDTH.group +
  WIDTH.code +
  WIDTH.length +
  WIDTH.width +
  sizeCount * WIDTH.size +
  WIDTH.status +
  (readOnly ? 0 : WIDTH.remove);

type OrderMarkerColumnsOptions = {
  sizes: string[];
  groups: OrderMarkerGroup[];
  readOnly: boolean;
  setRows: Dispatch<SetStateAction<OrderMarkerRow[]>>;
};

/**
 * Cột bảng bìa của một mã hàng: nhóm (kèm màu dùng nhóm, DEV-ALL-89: bìa dùng cho đúng các màu đó), mã, dài, số sản phẩm theo
 * từng size của đơn, trạng thái.
 */
export const orderMarkerColumns = ({
  sizes,
  groups,
  readOnly,
  setRows,
}: OrderMarkerColumnsOptions): TableColumnsType<OrderMarkerRow> => {
  const patch = (key: string, change: Partial<OrderMarkerRow>) => patchRow(setRows, key, change);
  const groupOptions = groups.map((g) => ({
    value: g.markerNo,
    label: fill(text.groupWithColors, g.markerNo, g.colors.join(', ')),
  }));
  return [
    {
      title: text.group,
      key: 'group',
      width: WIDTH.group,
      render: (_, r) => (
        <Select
          size="small"
          disabled={readOnly}
          aria-label={text.group}
          value={r.markerNo}
          options={groupOptions}
          style={FULL}
          onChange={(markerNo: number) => patch(r.key, { markerNo })}
        />
      ),
    },
    {
      title: text.code,
      key: 'code',
      width: WIDTH.code,
      render: (_, r) => (
        <Input
          size="small"
          disabled={readOnly}
          aria-label={text.code}
          maxLength={ORDER_MARKER_LIMITS.maxCodeLength}
          value={r.markerCode}
          style={FULL}
          onChange={(e) => patch(r.key, { markerCode: e.target.value })}
        />
      ),
    },
    {
      title: text.lengthM,
      key: 'length',
      width: WIDTH.length,
      render: (_, r) => (
        <QtyInput
          size="small"
          disabled={readOnly}
          aria-label={text.lengthM}
          min={0.01}
          max={ORDER_MARKER_LIMITS.maxLengthM}
          decimals={2}
          value={r.lengthM}
          style={FULL}
          onChange={(lengthM) => patch(r.key, { lengthM })}
        />
      ),
    },
    {
      title: text.widthM,
      key: 'width',
      width: WIDTH.width,
      render: (_, r) => {
        // DEV-ALL-104: bìa rộng hơn khổ vải của nhóm thì cảnh báo ngay khi nhập (không chặn lưu).
        const warning = tooWideWarning(r, groups);
        return (
          // QtyInput không nhận ref nên bọc thẻ div để Tooltip bám vị trí.
          <Tooltip title={warning || undefined}>
            <div>
              <QtyInput
                size="small"
                disabled={readOnly}
                aria-label={text.widthM}
                aria-description={warning || undefined}
                status={warning ? 'warning' : undefined}
                suffix={warning ? <WarningOutlined /> : undefined}
                min={WIDTH_MIN_M}
                max={WIDTH_MAX_M}
                decimals={WIDTH_DECIMALS}
                value={r.widthM}
                style={FULL}
                onChange={(widthM) => patch(r.key, { widthM })}
              />
            </div>
          </Tooltip>
        );
      },
    },
    ...sizes.map((size) => ({
      title: size,
      key: `size-${size}`,
      width: WIDTH.size,
      render: (_: unknown, r: OrderMarkerRow) => (
        <QtyInput
          size="small"
          disabled={readOnly}
          aria-label={`${text.code} ${r.markerCode} ${size}`}
          min={1}
          max={ORDER_MARKER_LIMITS.maxPiecesPerPly}
          value={piecesOf(r, size)}
          style={FULL}
          onChange={(value) => patch(r.key, withPieces(r, size, value))}
        />
      ),
    })),
    {
      title: text.status,
      key: 'status',
      width: WIDTH.status,
      render: (_, r) => (
        <Select
          size="small"
          disabled={readOnly}
          aria-label={text.status}
          value={r.status}
          options={STATUS_OPTIONS}
          style={FULL}
          onChange={(status: OrderMarkerStatus) => patch(r.key, { status })}
        />
      ),
    },
    // Bỏ ghim phải của cột xóa: bảng không cuộn bằng antd nên cột ghim chỉ làm antd đo lại vô ích.
    ...(readOnly
      ? []
      : [{ ...deleteRowColumn<OrderMarkerRow>((key) => dropRow(setRows, key)), fixed: undefined }]),
  ];
};
