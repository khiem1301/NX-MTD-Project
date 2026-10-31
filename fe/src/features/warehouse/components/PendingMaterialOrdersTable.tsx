import { Alert, Button, Space, Tag, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { formatDate, formatQty } from '@/utils/format';
import { INCOMING_TEXT } from '../constants';
import { usePendingMaterialOrders } from '../hooks/useOrderMaterials';
import type { PendingMaterialOrder } from '../types';
import { DataTable } from '@/components/common/DataTable';

const text = INCOMING_TEXT;

const statusTag = (o: PendingMaterialOrder) => {
  if (o.materialLines === 0) return <Tag color="red">{text.notDeclared}</Tag>;
  return o.materialStatus === 'Partial' ? (
    <Tag color="gold">{text.partial}</Tag>
  ) : (
    <Tag color="orange">{text.awaiting}</Tag>
  );
};

/**
 * Đơn đang chạy mà NVL chưa về đủ, đơn mới chưa khai báo NVL lên đầu để Quản đốc / Admin thấy ngay việc cần làm.
 * Bấm vào đơn thì mở NVL của đơn đó.
 */
export function PendingMaterialOrdersTable({ onPick }: { onPick: (orderId: number) => void }) {
  const pending = usePendingMaterialOrders();
  const rows = [...(pending.data ?? [])].sort(
    (a, b) =>
      Number(a.materialLines > 0) - Number(b.materialLines > 0) ||
      a.dueDate.localeCompare(b.dueDate),
  );

  const columns: TableColumnsType<PendingMaterialOrder> = [
    { title: text.order, dataIndex: 'orderCode', render: (v: string) => <b>{v}</b> },
    { title: text.customerName, dataIndex: 'customerName' },
    { title: text.dueDate, dataIndex: 'dueDate', render: formatDate },
    { title: 'NVL', key: 'status', render: (_, o) => statusTag(o) },
    { title: text.materialLines, dataIndex: 'materialLines', align: 'right' },
    { title: text.customerQty, dataIndex: 'customerQty', align: 'right', render: formatQty },
    { title: text.toBuyQty, dataIndex: 'toBuyQty', align: 'right', render: formatQty },
    {
      key: 'action',
      render: (_, o) => (
        <Button type="primary" size="small" onClick={() => onPick(o.orderId)}>
          {text.open}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={8} style={{ width: '100%', marginBottom: 24 }}>
      <Typography.Title level={5} style={{ margin: 0 }}>
        {text.pendingTitle}
      </Typography.Title>
      <Typography.Text type="secondary">{text.pendingHint}</Typography.Text>
      <QueryError error={pending.error} onRetry={() => void pending.refetch()} />
      {pending.data && pending.data.length === 0 && (
        <Alert type="success" showIcon message={text.pendingEmpty} />
      )}
      {rows.length > 0 && (
        <DataTable
          rowKey="orderId"
          columns={columns}
          dataSource={rows}
          loading={pending.isFetching}
          pagination={false}
        />
      )}
    </Space>
  );
}
