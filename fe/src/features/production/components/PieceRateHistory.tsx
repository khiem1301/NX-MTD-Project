import { Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { formatCurrency, formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useOperationRole } from '../hooks/useOperationRole';
import { usePieceRates } from '../hooks/usePieceRates';
import type { Operation, PieceRate } from '../types';
import { DataTable } from '@/components/common/DataTable';
import { DetailModal } from '@/components/common/DetailModal';

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

/** Hộp thoại lịch sử đơn giá của công đoạn đang xem. */
export function PieceRateHistoryModal({
  operation,
  onClose,
}: {
  operation: Operation | null;
  onClose: () => void;
}) {
  return (
    <DetailModal
      open={operation !== null}
      onClose={onClose}
      title={operation ? `${operation.operationCode} - ${operation.operationName}` : undefined}
      size="md"
    >
      {operation && <PieceRateHistory operationId={operation.operationId} />}
    </DetailModal>
  );
}

/** Lịch sử đơn giá của một công đoạn (mở rộng dòng trên màn công đoạn): Kế toán thấy mọi bản, Admin không thấy bản Nháp. */
export function PieceRateHistory({ operationId }: { operationId: number }) {
  const rates = usePieceRates(useOperationRole(), { page: 1, pageSize: 50, operationId });

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
