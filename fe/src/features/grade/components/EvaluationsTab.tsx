import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Space, Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { EVAL_STATUS, EVAL_STATUS_LABEL, GR_LABELS } from '../constants';
import { useEvaluations, useRejectGrade } from '../hooks/useGrade';
import type { GradeEvaluation } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import { ApplyModal } from './ApplyModal';
import { ProposeModal } from './ProposeModal';
import { SetGradeModal } from './SetGradeModal';

const STATUS_OPTIONS = Object.entries(EVAL_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

export function EvaluationsTab() {
  const { hasRole } = usePermission();
  const isLeader = hasRole(ROLES.TeamLeader, ROLES.TeamDeputy);
  const canDecide = hasRole(ROLES.Admin, ROLES.Manager);
  const table = useTableQuery<{ keyword?: string; status?: string }>({});
  const list = useEvaluations(table.params);
  const reject = useRejectGrade();
  const [proposeOpen, setProposeOpen] = useState(false);
  const [setOpen, setSetOpen] = useState(false);
  const [applying, setApplying] = useState<GradeEvaluation | null>(null);
  const [rejecting, setRejecting] = useState<GradeEvaluation | null>(null);

  const columns: TableColumnsType<GradeEvaluation> = [
    {
      title: GR_LABELS.worker,
      key: 'worker',
      render: (_, e) => `${e.workerCode} - ${e.workerName}`,
    },
    { title: GR_LABELS.team, dataIndex: 'teamName', render: (v: string | null) => v ?? '—' },
    {
      title: GR_LABELS.currentGrade,
      dataIndex: 'currentGradeCode',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: GR_LABELS.proposedGrade,
      dataIndex: 'proposedGradeCode',
      render: (v: string) => <Tag color="blue">{v}</Tag>,
    },
    { title: GR_LABELS.defects, dataIndex: 'recentDefectCount', align: 'right' },
    { title: GR_LABELS.reason, dataIndex: 'reason' },
    { title: GR_LABELS.proposedBy, dataIndex: 'proposedByName' },
    { title: GR_LABELS.date, dataIndex: 'evaluationDate', render: formatDate },
    {
      title: GR_LABELS.status,
      dataIndex: 'status',
      render: (v: string, e) => (
        <Space direction="vertical" size={0}>
          <StatusTag status={v} labels={EVAL_STATUS_LABEL} />
          {e.effectiveDate && (
            <span>{`${GR_LABELS.effectiveDate} ${formatDate(e.effectiveDate)}`}</span>
          )}
          {e.decisionNote && <span>{e.decisionNote}</span>}
        </Space>
      ),
    },
    ...(canDecide
      ? [
          {
            title: MESSAGES.common.actions,
            key: 'action',
            render: (_: unknown, e: GradeEvaluation) =>
              e.status === EVAL_STATUS.SUBMITTED ? (
                <Space size={0} wrap>
                  <Button type="link" onClick={() => setApplying(e)}>
                    {GR_LABELS.apply}
                  </Button>
                  <Button type="link" danger onClick={() => setRejecting(e)}>
                    {GR_LABELS.notApply}
                  </Button>
                </Space>
              ) : null,
          },
        ]
      : []),
  ];

  return (
    <>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 260 }}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={GR_LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 180 }}
            />
          </>
        }
        actions={
          <>
            {isLeader && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setProposeOpen(true)}>
                {GR_LABELS.propose}
              </Button>
            )}
            {canDecide && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setSetOpen(true)}>
                {GR_LABELS.setDirect}
              </Button>
            )}
          </>
        }
      />
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <DataTable
        rowKey="gradeEvaluationId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
      />
      <ProposeModal open={proposeOpen} onClose={() => setProposeOpen(false)} />
      <SetGradeModal open={setOpen} onClose={() => setSetOpen(false)} />
      <ApplyModal evaluation={applying} onClose={() => setApplying(null)} />
      <ReasonModal
        open={rejecting !== null}
        title={GR_LABELS.notApplyTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(null)}
        onSubmit={(reason) =>
          rejecting &&
          reject.mutate(
            { id: rejecting.gradeEvaluationId, reason },
            { onSuccess: () => setRejecting(null) },
          )
        }
      />
    </>
  );
}
