import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { Space, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { OrderMaterial } from '../types';
import { formatFabricWidth } from '../utils/fabricWidth';

type OrderMaterialColumnsOptions = {
  onEdit: (om: OrderMaterial) => void;
  onDelete: (om: OrderMaterial) => void;
};

const qtyWithUnit = (v: number, om: OrderMaterial) => `${formatQty(v)} ${om.unit}`;

const actionColumn = ({
  onEdit,
  onDelete,
}: OrderMaterialColumnsOptions): TableColumnsType<OrderMaterial>[number] => ({
  title: MESSAGES.common.actions,
  key: 'actions',
  fixed: 'right',
  render: (_, om) => (
    <Space size={0} wrap>
      <IconAction title={MESSAGES.common.edit} icon={<EditOutlined />} onClick={() => onEdit(om)} />
      <ConfirmAction
        title={MESSAGES.warehouse.confirmDeleteOrderMaterial}
        onConfirm={() => onDelete(om)}
      >
        <IconAction title={MESSAGES.common.delete} icon={<DeleteOutlined />} danger />
      </ConfirmAction>
    </Space>
  ),
});

/** Cột bảng NVL của đơn: NVL, số lượng theo nguồn cấp, tiến độ nhập kho; không truyền thao tác (đơn đã đóng) thì không có cột thao tác. */
export const orderMaterialColumns = (
  actions?: OrderMaterialColumnsOptions,
): TableColumnsType<OrderMaterial> => [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  {
    title: LABELS.materialWidthM,
    dataIndex: 'materialWidthCm',
    render: (v: number | null) => formatFabricWidth(v),
  },
  { title: LABELS.requiredQty, dataIndex: 'requiredQty', align: 'right', render: qtyWithUnit },
  { title: LABELS.customerQty, dataIndex: 'customerQty', align: 'right', render: qtyWithUnit },
  { title: LABELS.purchaseQty, dataIndex: 'purchaseQty', align: 'right', render: qtyWithUnit },
  {
    title: LABELS.deliveries,
    key: 'deliveries',
    align: 'right',
    render: (_, om) =>
      om.expectedDeliveryCount === null
        ? '—'
        : `${om.receivedCount} / ${om.expectedDeliveryCount} ${LABELS.deliveryUnit}`,
  },
  {
    title: LABELS.receivedQty,
    dataIndex: 'receivedQty',
    align: 'right',
    render: (v: number, om) => (
      <Typography.Text type={v >= om.requiredQty && om.requiredQty > 0 ? 'success' : undefined}>
        {qtyWithUnit(v, om)}
      </Typography.Text>
    ),
  },
  ...(actions ? [actionColumn(actions)] : []),
];
