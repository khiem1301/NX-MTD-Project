import { useState } from 'react';
import { Button, Space, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { ProductSelect } from '../components/ProductSelect';
import { PRODUCTION_TEXT } from '../constants';
import {
  useApproveManyPieceRates,
  useApprovePieceRate,
  usePieceRates,
  useRejectPieceRate,
} from '../hooks/useProduction';
import type { PieceRate, PieceRateQuery, PieceRateStatus } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';

const text = PRODUCTION_TEXT.pieceRates;

/** Admin không thấy đơn giá Nháp (chưa gửi duyệt): tab "Tất cả" gồm các trạng thái còn lại. */
const STATUS_OPTIONS = (['PendingApproval', 'Approved', 'Rejected'] as PieceRateStatus[]).map(
  (value) => ({
    value,
    label: text.status[value].text,
  }),
);

/**
 * UC26 (KI-01) — Admin duyệt hoặc từ chối đơn giá công đoạn do Kế toán gửi (từ chối bắt buộc lý do). Mặc định xem các
 * đơn giá đang chờ duyệt. Kế toán đặt và gửi duyệt đơn giá ngay trên màn "Công đoạn và đơn giá".
 */
export function AdminPieceRatesPage() {
  const [query, setQuery] = useState<PieceRateQuery>({ page: 1, pageSize: 100, status: undefined });
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [approvingMany, setApprovingMany] = useState(false);
  const [approving, setApproving] = useState<PieceRate | null>(null);
  const [rejecting, setRejecting] = useState<PieceRate | null>(null);
  const rates = usePieceRates('admin', query);
  const approve = useApprovePieceRate();
  const reject = useRejectPieceRate();
  const approveMany = useApproveManyPieceRates();

  const columns: ColumnsType<PieceRate> = [
    { title: text.columns.product, dataIndex: 'productCode' },
    {
      title: text.columns.operation,
      key: 'op',
      render: (_, r) => `${r.operationCode} - ${r.operationName}`,
    },
    {
      title: text.columns.rate,
      dataIndex: 'rate',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.columns.effectiveDate,
      dataIndex: 'effectiveDate',
      render: (v: string) => formatDate(v),
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
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) =>
        row.status === 'PendingApproval' ? (
          <Space size={0} wrap>
            <Button type="link" onClick={() => setApproving(row)}>
              {text.approve}
            </Button>
            <Button type="link" danger onClick={() => setRejecting(row)}>
              {text.reject}
            </Button>
          </Space>
        ) : null,
    },
  ];

  return (
    <PageCard title={text.adminTitle} description={text.adminSubtitle}>
      <TableToolbar
        filters={
          <>
            <ProductSelect
              allowClear
              value={query.productId}
              placeholder={PRODUCTION_TEXT.selectProduct}
              onChange={(productId) => setQuery({ ...query, page: 1, productId })}
            />
            <FilterSelect<PieceRateStatus>
              value={query.status}
              placeholder={text.columns.status}
              style={{ width: 200 }}
              options={STATUS_OPTIONS}
              onChange={(status) => {
                setSelectedIds([]);
                setQuery({ ...query, page: 1, status });
              }}
            />
          </>
        }
        actions={
          <Button
            type="primary"
            disabled={selectedIds.length === 0}
            onClick={() => setApprovingMany(true)}
          >
            {`${text.approveSelected} (${selectedIds.length})`}
          </Button>
        }
      />

      <QueryError error={rates.error} onRetry={() => void rates.refetch()} />

      <DataTable<PieceRate>
        rowKey="pieceRateId"
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: (keys) => setSelectedIds(keys as number[]),
          getCheckboxProps: (row) => ({ disabled: row.status !== 'PendingApproval' }),
        }}
        size="small"
        columns={columns}
        dataSource={rates.data?.items}
        loading={rates.isFetching}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: rates.data?.page ?? query.page,
          pageSize: rates.data?.pageSize ?? query.pageSize,
          total: rates.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [20, 50, 100],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => {
            setSelectedIds([]);
            setQuery({ ...query, page, pageSize });
          },
        }}
      />

      <NoteModal
        open={approving !== null}
        title={text.approveTitle}
        label={text.note}
        required={false}
        loading={approve.isPending}
        onCancel={() => setApproving(null)}
        onSubmit={(note) =>
          approving &&
          approve.mutate(
            { id: approving.pieceRateId, note: note ?? undefined },
            { onSuccess: () => setApproving(null) },
          )
        }
      />
      <NoteModal
        open={approvingMany}
        title={text.approveSelectedTitle}
        label={text.note}
        required={false}
        loading={approveMany.isPending}
        onCancel={() => setApprovingMany(false)}
        onSubmit={(note) =>
          approveMany.mutate(
            { ids: selectedIds, note: note ?? undefined },
            {
              onSuccess: (count) => {
                void message.success(`Đã duyệt ${count} đơn giá.`);
                setSelectedIds([]);
                setApprovingMany(false);
              },
            },
          )
        }
      />
      <ReasonModal
        open={rejecting !== null}
        title={text.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(null)}
        onSubmit={(reason) =>
          rejecting &&
          reject.mutate(
            { id: rejecting.pieceRateId, reason },
            { onSuccess: () => setRejecting(null) },
          )
        }
      />
    </PageCard>
  );
}
