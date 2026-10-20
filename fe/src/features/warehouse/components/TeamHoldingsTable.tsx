import { useState } from 'react';
import { Space, Table, type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useTeamHoldings } from '../hooks/useInventory';
import type { TeamPieceHolding } from '../types';

const columns: TableColumnsType<TeamPieceHolding> = [
  { title: LABELS.team, dataIndex: 'teamName' },
  { title: LABELS.order, dataIndex: 'orderCode' },
  { title: LABELS.product, dataIndex: 'productCode' },
  { title: LABELS.garmentColor, dataIndex: 'color' },
  { title: LABELS.sizeCode, dataIndex: 'sizeCode' },
  { title: LABELS.receivedFromWarehouse, dataIndex: 'receivedFromWarehouse', align: 'right' },
  { title: LABELS.handedToWorkers, dataIndex: 'handedToWorkers', align: 'right' },
  { title: LABELS.retrievedFromWorkers, dataIndex: 'retrievedFromWorkers', align: 'right' },
  {
    title: LABELS.heldByTeam,
    dataIndex: 'heldByTeam',
    align: 'right',
    render: (v: number) => <b>{v}</b>,
  },
];

/** BTP tổ may đang giữ theo dòng đơn (Thủ kho / Quản đốc xem mọi tổ, tổ trưởng may xem tổ mình). */
export function TeamHoldingsTable() {
  const [orderId, setOrderId] = useState<number>();
  const holdings = useTeamHoldings(orderId);
  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <OrderSelect allowClear placeholder={LABELS.order} value={orderId} onChange={setOrderId} />
      </Space>
      <QueryError error={holdings.error} onRetry={() => void holdings.refetch()} />
      <Table
        rowKey={(h) => `${h.sewingTeamId}|${h.orderLineId}`}
        columns={columns}
        dataSource={holdings.data}
        loading={holdings.isFetching}
        pagination={false}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
    </>
  );
}
