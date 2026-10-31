import { useState } from 'react';
import { Space, type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { LABELS } from '../constants';
import { useTeamHoldings } from '../hooks/useInventory';
import type { TeamPieceHolding } from '../types';
import { DataTable } from '@/components/common/DataTable';

const columns: TableColumnsType<TeamPieceHolding> = [
  { title: LABELS.team, dataIndex: 'teamName' },
  { title: LABELS.order, dataIndex: 'orderCode' },
  { title: LABELS.product, dataIndex: 'productCode' },
  { title: LABELS.garmentColor, dataIndex: 'color' },
  { title: LABELS.sizeCode, dataIndex: 'sizeCode' },
  { title: LABELS.pieceReceivedQty, dataIndex: 'receivedQty', align: 'right' },
  { title: LABELS.finishedQty, dataIndex: 'finishedQty', align: 'right' },
  { title: LABELS.scrapQty, dataIndex: 'scrapQty', align: 'right' },
  {
    title: LABELS.onLineQty,
    dataIndex: 'onLineQty',
    align: 'right',
    render: (v: number) => <b>{v}</b>,
  },
];

/** BTP tổ may theo dòng đơn: số bộ đã nhận từ kho, QC đạt, hàng hủy, còn trên chuyền (Thủ kho / Quản đốc xem mọi tổ, tổ trưởng may xem tổ mình). */
export function TeamHoldingsTable() {
  const [orderId, setOrderId] = useState<number>();
  const holdings = useTeamHoldings(orderId);
  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <OrderSelect allowClear placeholder={LABELS.order} value={orderId} onChange={setOrderId} />
      </Space>
      <QueryError error={holdings.error} onRetry={() => void holdings.refetch()} />
      <DataTable
        rowKey={(h) => `${h.sewingTeamId}|${h.orderLineId}`}
        columns={columns}
        dataSource={holdings.data}
        loading={holdings.isFetching}
        pagination={false}
      />
    </>
  );
}
