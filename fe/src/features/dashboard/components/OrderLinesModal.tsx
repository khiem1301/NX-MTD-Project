import { Alert, Spin, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DetailModal } from '@/components/common/DetailModal';
import { useOrderLines } from '../hooks/useOrderProgress';
import type { LineProgress, OrderProgress } from '../types';

export function OrderLinesModal({
  order,
  onClose,
}: {
  order: OrderProgress | null;
  onClose: () => void;
}) {
  const lines = useOrderLines(order?.orderId);
  const columns: TableColumnsType<LineProgress> = [
    {
      title: 'Mã hàng - màu - size',
      key: 'line',
      render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
    },
    { title: 'Đặt', dataIndex: 'orderedQty', align: 'right' },
    { title: 'Cắt', dataIndex: 'cutQty', align: 'right' },
    { title: 'Vào kho BTP', dataIndex: 'cutHandedToWarehouseQty', align: 'right' },
    { title: 'Cấp tổ may', dataIndex: 'issuedToSewingQty', align: 'right' },
    { title: 'Hoàn thiện', dataIndex: 'finishedQty', align: 'right' },
    { title: 'Nhập kho TP', dataIndex: 'finishedReceivedQty', align: 'right' },
    { title: 'Đã giao', dataIndex: 'deliveredQty', align: 'right' },
    { title: 'Rework', dataIndex: 'reworkQty', align: 'right' },
    { title: 'Scrap', dataIndex: 'scrapQty', align: 'right' },
    {
      title: 'Tỉ lệ lỗi',
      dataIndex: 'defectRatePct',
      align: 'right',
      render: (v: number | null) => `${v ?? 0}%`,
    },
  ];
  return (
    <DetailModal
      open={order !== null}
      onClose={onClose}
      title={order ? `Tiến độ ${order.orderCode}` : ''}
      size="lg"
    >
      {lines.isLoading && <Spin />}
      {lines.error && <Alert type="error" message={lines.error.message} />}
      <DataTable
        rowKey="orderLineId"
        columns={columns}
        dataSource={lines.data}
        pagination={false}
      />
    </DetailModal>
  );
}
