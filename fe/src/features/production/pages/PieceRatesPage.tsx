import { useState } from 'react';
import { Button, Popconfirm, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { PieceRateFormModal } from '../components/PieceRateFormModal';
import { ProductSelect } from '../components/ProductSelect';
import { PRODUCTION_TEXT } from '../constants';
import { useApprovePieceRate, usePieceRates, useRejectPieceRate, useSubmitPieceRate } from '../hooks/useProduction';
import type { PieceRate, PieceRateQuery, PieceRateRole, PieceRateStatus } from '../types';

const text = PRODUCTION_TEXT.pieceRates;

const STATUS_OPTIONS = (Object.keys(text.status) as PieceRateStatus[]).map((value) => ({
  value,
  label: text.status[value].text,
}));

/**
 * UC25 / UC26 (KI-01) — danh sách đơn giá công đoạn. Kế toán lập, sửa (Nháp / bị từ chối), gửi duyệt;
 * Admin duyệt hoặc từ chối (bắt buộc lý do). Admin mặc định xem các đơn giá đang chờ duyệt.
 */
function PieceRateBoard({ role }: { role: PieceRateRole }) {
  const isAdmin = role === 'admin';
  const [query, setQuery] = useState<PieceRateQuery>({
    page: 1,
    pageSize: 20,
    status: isAdmin ? 'PendingApproval' : undefined,
  });
  const [editing, setEditing] = useState<PieceRate | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [approving, setApproving] = useState<PieceRate | null>(null);
  const [rejecting, setRejecting] = useState<PieceRate | null>(null);
  const rates = usePieceRates(role, query);
  const submit = useSubmitPieceRate();
  const approve = useApprovePieceRate();
  const reject = useRejectPieceRate();

  const openForm = (row: PieceRate | null) => {
    setEditing(row);
    setFormOpen(true);
  };

  const columns: ColumnsType<PieceRate> = [
    { title: text.columns.product, dataIndex: 'productCode' },
    { title: text.columns.operation, key: 'op', render: (_, r) => `${r.operationCode} - ${r.operationName}` },
    { title: text.columns.rate, dataIndex: 'rate', align: 'right', render: (v: number) => formatCurrency(v) },
    { title: text.columns.effectiveDate, dataIndex: 'effectiveDate', render: (v: string) => formatDate(v) },
    { title: text.columns.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={text.status} /> },
    {
      title: text.columns.note,
      key: 'note',
      render: (_, r) => r.rejectReason ?? r.approvalNote ?? '—',
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0} wrap>
          {!isAdmin && (row.status === 'Draft' || row.status === 'Rejected') && (
            <Button type="link" onClick={() => openForm(row)}>
              {MESSAGES.common.edit}
            </Button>
          )}
          {!isAdmin && row.status === 'Draft' && (
            <Popconfirm title={text.submitConfirm} onConfirm={() => submit.mutate(row.pieceRateId)}>
              <Button type="link">{text.submit}</Button>
            </Popconfirm>
          )}
          {isAdmin && row.status === 'PendingApproval' && (
            <>
              <Button type="link" onClick={() => setApproving(row)}>
                {text.approve}
              </Button>
              <Button type="link" danger onClick={() => setRejecting(row)}>
                {text.reject}
              </Button>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {isAdmin ? text.adminTitle : text.accountantTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{isAdmin ? text.adminSubtitle : text.accountantSubtitle}</Typography.Text>
      </div>

      <Space wrap>
        <ProductSelect
          allowClear
          value={query.productId}
          placeholder={PRODUCTION_TEXT.selectProduct}
          onChange={(productId) => setQuery({ ...query, page: 1, productId })}
        />
        <Select<PieceRateStatus | undefined>
          allowClear
          placeholder={text.allStatus}
          style={{ width: 180 }}
          value={query.status}
          options={STATUS_OPTIONS}
          onChange={(status) => setQuery({ ...query, page: 1, status })}
        />
        {!isAdmin && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
            {text.add}
          </Button>
        )}
      </Space>

      <QueryError error={rates.error} onRetry={() => void rates.refetch()} />

      <Table<PieceRate>
        rowKey="pieceRateId"
        size="middle"
        columns={columns}
        dataSource={rates.data?.items}
        loading={rates.isFetching}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: rates.data?.page ?? query.page,
          pageSize: rates.data?.pageSize ?? query.pageSize,
          total: rates.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => setQuery({ ...query, page, pageSize }),
        }}
      />

      {!isAdmin && <PieceRateFormModal open={formOpen} pieceRate={editing} onClose={() => setFormOpen(false)} />}

      <NoteModal
        open={approving !== null}
        title={text.approveTitle}
        label={text.note}
        required={false}
        loading={approve.isPending}
        onCancel={() => setApproving(null)}
        onSubmit={(note) =>
          approving &&
          approve.mutate({ id: approving.pieceRateId, note: note ?? undefined }, { onSuccess: () => setApproving(null) })
        }
      />
      <ReasonModal
        open={rejecting !== null}
        title={text.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(null)}
        onSubmit={(reason) =>
          rejecting && reject.mutate({ id: rejecting.pieceRateId, reason }, { onSuccess: () => setRejecting(null) })
        }
      />
    </Space>
  );
}

/** Kế toán: lập đơn giá và gửi duyệt. */
export const PieceRatesPage = () => <PieceRateBoard role="accountant" />;

/** Admin: duyệt hoặc từ chối đơn giá. */
export const AdminPieceRatesPage = () => <PieceRateBoard role="admin" />;
