import { CopyOutlined, EyeOutlined } from '@ant-design/icons';
import { Space, Tag, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import {
  MATERIAL_STATUS_TAG,
  ORDER_STATUS_LABEL,
  ORDER_STATUS_TAG,
  type OrderStatus,
} from '@/constants/orderStatus';
import { formatDate, formatQty } from '@/utils/format';
import type { OrderOverviewListItem } from '../types';

export const ORDER_STATUS_OPTIONS = (Object.keys(ORDER_STATUS_LABEL) as OrderStatus[]).map((v) => ({
  value: v,
  label: ORDER_STATUS_LABEL[v],
}));

type OrderListColumnsOptions<T> = {
  onView: (order: T) => void;
  /** Admin: mở form tạo đơn mới với dữ liệu copy từ đơn này. */
  onCopy?: (order: T) => void;
  /** Cột chèn sau Tổng SL (Admin: thành tiền CMT). */
  extraColumns?: TableColumnsType<T>;
};

/** Cột danh sách đơn hàng dùng chung cho Admin và Quản đốc. */
export const orderListColumns = <T extends OrderOverviewListItem>({
  onView,
  onCopy,
  extraColumns = [],
}: OrderListColumnsOptions<T>): TableColumnsType<T> => [
  { title: MESSAGES.orders.orderCode, dataIndex: 'orderCode' },
  { title: MESSAGES.orders.customer, dataIndex: 'customerName' },
  { title: MESSAGES.orders.productCode, dataIndex: 'productCodes', ellipsis: true },
  { title: MESSAGES.orders.receivedDate, dataIndex: 'receivedDate', render: formatDate },
  { title: MESSAGES.orders.dueDate, dataIndex: 'dueDate', render: formatDate },
  { title: MESSAGES.orders.totalQty, dataIndex: 'totalQty', align: 'right', render: formatQty },
  ...extraColumns,
  {
    title: MESSAGES.orders.status,
    dataIndex: 'status',
    render: (v: string, o) => (
      <Space size={4}>
        <StatusTag status={v} labels={ORDER_STATUS_TAG} />
        {o.isDelayRisk && <Tag color="red">{MESSAGES.orders.delayRisk}</Tag>}
      </Space>
    ),
  },
  {
    title: MESSAGES.orders.materialStatus,
    dataIndex: 'materialStatus',
    render: (v: string) => <StatusTag status={v} labels={MATERIAL_STATUS_TAG} />,
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, o) => (
      <Space size={0}>
        <IconAction title={MESSAGES.common.view} icon={<EyeOutlined />} onClick={() => onView(o)} />
        {onCopy && (
          <IconAction
            title={MESSAGES.orders.copyOrder}
            icon={<CopyOutlined />}
            onClick={() => onCopy(o)}
          />
        )}
      </Space>
    ),
  },
];
