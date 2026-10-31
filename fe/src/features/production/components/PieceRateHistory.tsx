import { Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { formatCurrency, formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { usePieceRates } from '../hooks/useProduction';
import type { PieceRate } from '../types';
import { DataTable } from '@/components/common/DataTable';

const text = PRODUCTION_TEXT.pieceRates;

const columns: ColumnsType<PieceRate> = [
  {
    title: text.columns.effectiveDate,
    dataIndex: 'effectiveDate',
    render: (v: string) => formatDate(v),
  },
  {
    title: text.columns.rate,
    dataIndex: 'rate',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: text.columns.status,
    dataIndex: 'status',
    render: (v: string) => <StatusTag status={v} labels={text.status} />,
  },
  {
    title: text.columns.note,
    key: 'note',
    render: (_, r) => r.rejectReason ?? r.approvalNote ?? '—',
  },
];

/** Lịch sử đơn giá của một công đoạn (mở rộng dòng trên màn công đoạn): mọi bản, kể cả Nháp và bị từ chối. */
export function PieceRateHistory({ operationId }: { operationId: number }) {
  const rates = usePieceRates('accountant', { page: 1, pageSize: 50, operationId });

  return (
    <>
      <Typography.Text strong>{text.history}</Typography.Text>
      <QueryError error={rates.error} onRetry={() => void rates.refetch()} />
      <DataTable<PieceRate>
        rowKey="pieceRateId"
        size="small"
        columns={columns}
        dataSource={rates.data?.items}
        loading={rates.isFetching}
        locale={{ emptyText: text.noHistory }}
        pagination={false}
      />
    </>
  );
}
