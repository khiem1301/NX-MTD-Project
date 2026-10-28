import { useState } from 'react';
import { Button, DatePicker, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { QueryError } from '@/components/common/QueryError';
import { REQUEST_STATUS_LABEL, type RequestStatus } from '@/constants/requestTypes';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate, formatDateTime, toApiDate } from '@/utils/format';
import { DefectFormModal } from '../components/DefectFormModal';
import { ReplacementCuttingModal } from '../components/ReplacementCuttingModal';
import { PRODUCTION_TEXT } from '../constants';
import { useCancelDefect, useDefects } from '../hooks/useProduction';
import type { Defect, DefectQuery, OutputRole } from '../types';

const text = PRODUCTION_TEXT.defects;

/**
 * UC56 (KI-08) — phiếu hàng hỏng của tổ. Tổ trưởng / tổ phó ghi và hủy phiếu trong hạn; Quản đốc xem các tổ
 * và hủy phiếu ghi sai, kể cả sau hạn (bắt buộc lý do, có nhật ký).
 */
function DefectBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  const [query, setQuery] = useState<DefectQuery>({ page: 1, pageSize: 20 });
  const [formOpen, setFormOpen] = useState(false);
  const [cancelling, setCancelling] = useState<Defect | null>(null);
  const [replacingPick, setReplacing] = useState<Defect | null>(null);
  // Vừa ghi hàng hủy (Scrap): mở luôn đơn cắt bù cho Quản đốc (swimlane III.3.9–III.3.10); phiếu lấy từ danh sách sau khi tải lại.
  const [autoReplaceId, setAutoReplaceId] = useState<number | null>(null);
  const defects = useDefects(role, query);
  const replacing =
    replacingPick ?? (autoReplaceId === null ? null : (defects.data?.items.find((d) => d.defectRecordId === autoReplaceId) ?? null));
  const cancel = useCancelDefect(role);

  const columns: ColumnsType<Defect> = [
    { title: text.columns.code, dataIndex: 'defectCode' },
    { title: text.columns.date, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
    ...(isManager ? [{ title: text.columns.team, dataIndex: 'sewingTeamName' }] : []),
    { responsive: ['md'], title: text.columns.order, key: 'order', render: (_, d) => `${d.orderCode} · ${d.productCode}` },
    { title: text.columns.operation, key: 'op', render: (_, d) => `${d.operationCode} - ${d.operationName}` },
    { title: text.columns.worker, dataIndex: 'responsibleWorkerName' },
    {
      title: text.columns.type,
      dataIndex: 'defectType',
      render: (v: string) => <StatusTag status={v} labels={text.type} />,
    },
    { title: text.columns.qty, dataIndex: 'quantity', align: 'right' },
    {
      title: text.columns.reason,
      key: 'reason',
      render: (_, d) => (d.reasonNote ? `${text.reason[d.defectReasonCode]}: ${d.reasonNote}` : text.reason[d.defectReasonCode]),
    },
    {
      responsive: ['md'],
      title: text.columns.rate,
      dataIndex: 'scrapUnitRate',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    { title: text.columns.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={text.status} /> },
    {
      title: text.columns.objection,
      key: 'objection',
      render: (_, d) =>
        d.workerObjection ? (
          <Space direction="vertical" size={0}>
            <Tag color="volcano">{text.objected}</Tag>
            <Typography.Text>{d.workerObjection}</Typography.Text>
          </Space>
        ) : (
          '—'
        ),
    },
    { responsive: ['md'], title: text.columns.deadline, dataIndex: 'editDeadline', render: (v: string) => formatDateTime(v) },
    {
      title: PRODUCTION_TEXT.replacement.column,
      key: 'replacement',
      render: (_, d) => {
        if (d.replacementRequestCode)
          return (
            <Tag color="geekblue">
              {d.replacementRequestCode} · {REQUEST_STATUS_LABEL[d.replacementRequestStatus as RequestStatus] ?? d.replacementRequestStatus}
            </Tag>
          );
        return !isManager && d.defectType === 'Scrap' && d.status === 'Recorded' ? (
          <Button type="link" onClick={() => setReplacing(d)}>
            {PRODUCTION_TEXT.replacement.button}
          </Button>
        ) : (
          '—'
        );
      },
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, d) =>
        d.status === 'Recorded' && (isManager || d.canEdit) ? (
          <Button type="link" danger onClick={() => setCancelling(d)}>
            {text.cancel}
          </Button>
        ) : null,
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {isManager ? text.managerTitle : text.title}
        </Typography.Title>
        <Typography.Text type="secondary">{isManager ? text.managerSubtitle : text.subtitle}</Typography.Text>
      </div>

      <Space wrap>
        <DebouncedSearch
          allowClear
          style={{ width: 320 }}
          placeholder={text.searchPlaceholder}
          onSearch={(keyword) => setQuery({ ...query, page: 1, keyword: keyword.trim() || undefined })}
        />
        <DatePicker
          allowClear
          format="DD/MM/YYYY"
          placeholder={text.filterDate}
          onChange={(v: Dayjs | null) => setQuery({ ...query, page: 1, workDate: v ? toApiDate(v) : undefined })}
        />
        {!isManager && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>
            {text.add}
          </Button>
        )}
      </Space>

      <QueryError error={defects.error} onRetry={() => void defects.refetch()} />

      <Table<Defect>
        rowKey="defectRecordId"
        size="middle"
        columns={columns}
        dataSource={defects.data?.items}
        loading={defects.isFetching}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: defects.data?.page ?? query.page,
          pageSize: defects.data?.pageSize ?? query.pageSize,
          total: defects.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => setQuery({ ...query, page, pageSize }),
        }}
      />

      {!isManager && (
        <DefectFormModal
          open={formOpen}
          onClose={() => setFormOpen(false)}
          onCreated={(id, type) => type === 'Scrap' && setAutoReplaceId(id)}
        />
      )}
      {!isManager && <ReplacementCuttingModal
          defect={replacing}
          onClose={() => {
            setReplacing(null);
            setAutoReplaceId(null);
          }}
        />}
      <ReasonModal
        open={cancelling !== null}
        title={text.cancelTitle}
        loading={cancel.isPending}
        onCancel={() => setCancelling(null)}
        onSubmit={(reason) =>
          cancelling &&
          cancel.mutate(
            { id: cancelling.defectRecordId, body: { reason, rowVer: cancelling.rowVer } },
            { onSuccess: () => setCancelling(null) },
          )
        }
      />
    </Space>
  );
}

export const DefectRecordsPage = () => <DefectBoard role="team-leader" />;
export const ManagerDefectsPage = () => <DefectBoard role="manager" />;
