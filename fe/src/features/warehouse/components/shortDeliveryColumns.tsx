import { Form, Space, Typography, type TableColumnsType } from 'antd';
import { formatQty } from '@/utils/format';
import { QtyInput } from '@/components/common/QtyInput';
import { MATERIAL_QTY_DECIMALS } from '@/constants/quantity';
import { LABELS } from '../constants';
import type { ShortDeliveryLine } from '../types';

type ShortDeliveryColumnsOptions = {
  /** Quản đốc đang chọn số lượng bên giao bù. */
  canEditReplenish: boolean;
  /** Thủ kho đang nhận hàng bù: hiện ô số lượng nhận. */
  canReceive: boolean;
  replenishOf: (line: ShortDeliveryLine) => number;
  remaining: (line: ShortDeliveryLine) => number;
  onReplenishChange: (line: ShortDeliveryLine, qty: number) => void;
};

const withUnit = (line: ShortDeliveryLine, qty: number) => `${formatQty(qty)} ${line.unit}`;

/** Cột các dòng thiếu của yêu cầu giao thiếu NVL: số thiếu, số bên giao bù (Quản đốc chọn), số đã bù và ô nhận hàng bù (Thủ kho). */
export const shortDeliveryColumns = ({
  canEditReplenish,
  canReceive,
  replenishOf,
  remaining,
  onReplenishChange,
}: ShortDeliveryColumnsOptions): TableColumnsType<ShortDeliveryLine> => [
  {
    title: LABELS.material,
    key: 'material',
    render: (_, l) => (
      <Space direction="vertical" size={0}>
        <b>{l.materialCode}</b>
        <Typography.Text type="secondary">
          {l.materialName}
          {l.materialColor ? ` - ${l.materialColor}` : ''}
        </Typography.Text>
      </Space>
    ),
  },
  {
    title: LABELS.shortfallQty,
    key: 'shortfallQty',
    align: 'right',
    render: (_, l) => withUnit(l, l.shortfallQty),
  },
  ...(canEditReplenish
    ? [
        {
          title: LABELS.replenishQty,
          key: 'replenish',
          render: (_: unknown, l: ShortDeliveryLine) => (
            <QtyInput
              decimals={MATERIAL_QTY_DECIMALS}
              min={0}
              max={Math.max(0, l.shortfallQty - l.replenishedQty)}
              value={replenishOf(l)}
              addonAfter={l.unit}
              style={{ width: 150 }}
              onChange={(v) => onReplenishChange(l, v ?? 0)}
            />
          ),
        },
      ]
    : [
        {
          title: LABELS.replenishQty,
          key: 'replenishQty',
          align: 'right' as const,
          render: (_: unknown, l: ShortDeliveryLine) =>
            l.replenishQty === null ? '—' : withUnit(l, l.replenishQty),
        },
        {
          title: LABELS.replenishedQty,
          key: 'replenishedQty',
          align: 'right' as const,
          render: (_: unknown, l: ShortDeliveryLine) => withUnit(l, l.replenishedQty),
        },
      ]),
  ...(canReceive
    ? [
        {
          title: LABELS.acceptedQty,
          key: 'accepted',
          align: 'right' as const,
          render: (_: unknown, l: ShortDeliveryLine) => (
            <Form.Item
              name={['lines', l.requestLineId, 'acceptedQty']}
              noStyle
              initialValue={remaining(l)}
            >
              <QtyInput
                decimals={MATERIAL_QTY_DECIMALS}
                min={0}
                max={remaining(l)}
                addonAfter={l.unit}
                style={{ width: 160 }}
              />
            </Form.Item>
          ),
        },
      ]
    : []),
];
