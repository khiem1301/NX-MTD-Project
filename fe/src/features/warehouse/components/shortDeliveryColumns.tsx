import { Form, Space, type TableColumnsType } from 'antd';
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

/** Cột các dòng thiếu của yêu cầu giao thiếu NVL: số thiếu, số bên giao bù (Quản đốc chọn), số đã bù và ô nhận hàng bù (Thủ kho). */
export const shortDeliveryColumns = ({
  canEditReplenish,
  canReceive,
  replenishOf,
  remaining,
  onReplenishChange,
}: ShortDeliveryColumnsOptions): TableColumnsType<ShortDeliveryLine> => [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
  { title: LABELS.shortfallQty, dataIndex: 'shortfallQty', align: 'right', render: formatQty },
  ...(canEditReplenish
    ? [
        {
          title: LABELS.replenishQty,
          key: 'replenish',
          render: (_: unknown, l: ShortDeliveryLine) => (
            <QtyInput
              decimals={MATERIAL_QTY_DECIMALS}
              min={0}
              max={l.shortfallQty}
              value={replenishOf(l)}
              onChange={(v) => onReplenishChange(l, v ?? 0)}
            />
          ),
        },
      ]
    : [
        {
          title: LABELS.replenishQty,
          dataIndex: 'replenishQty',
          align: 'right' as const,
          render: (v: number | null) => (v === null ? '—' : formatQty(v)),
        },
        {
          title: LABELS.replenishedQty,
          dataIndex: 'replenishedQty',
          align: 'right' as const,
          render: formatQty,
        },
      ]),
  ...(canReceive
    ? [
        {
          title: LABELS.acceptedQty,
          key: 'accepted',
          render: (_: unknown, l: ShortDeliveryLine) => (
            <Space>
              <Form.Item
                name={['lines', l.materialReceiptLineId, 'acceptedQty']}
                noStyle
                initialValue={remaining(l)}
              >
                <QtyInput
                  decimals={MATERIAL_QTY_DECIMALS}
                  min={0}
                  max={remaining(l)}
                  addonBefore={LABELS.acceptedQty}
                  style={{ width: 150 }}
                />
              </Form.Item>
            </Space>
          ),
        },
      ]
    : []),
];
