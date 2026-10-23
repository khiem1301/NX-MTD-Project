import { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  type TableColumnsType,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatCurrency, formatDate } from '@/utils/format';
import { EVAL_STATUS, EVAL_STATUS_LABEL, GR_LABELS, GRADE_OPTIONS } from '../constants';
import {
  useAddLevel,
  useApplyGrade,
  useEvaluations,
  useGradeLevels,
  useGradeWorkers,
  useProposeGrade,
  useRejectGrade,
} from '../hooks/useGrade';
import type { GradeEvaluation, GradeLevel } from '../types';

const API_DATE = 'YYYY-MM-DD';
const required = [{ required: true, message: MESSAGES.common.required }];
const STATUS_OPTIONS = Object.entries(EVAL_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

function ProposeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<{ workerId: number; proposedGradeCode: string; reason: string }>();
  const workers = useGradeWorkers(open);
  const propose = useProposeGrade();
  return (
    <Modal
      open={open}
      title={GR_LABELS.propose}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={propose.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={propose.isPending}
        onFinish={(v) => propose.mutate(v, { onSuccess: onClose })}
      >
        <Form.Item name="workerId" label={GR_LABELS.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={GR_LABELS.selectWorker}
            loading={workers.isLoading}
            options={(workers.data ?? []).map((w) => ({
              value: w.workerId,
              disabled: w.hasPending,
              label: `${w.workerCode} - ${w.fullName} · ${GR_LABELS.currentGrade}: ${w.currentGradeCode ?? GR_LABELS.none} · ${GR_LABELS.defects}: ${w.recentDefectCount}${w.hasPending ? ` (${GR_LABELS.pending})` : ''}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="proposedGradeCode" label={GR_LABELS.proposedGrade} rules={required}>
          <Select options={GRADE_OPTIONS} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item
          name="reason"
          label={GR_LABELS.reason}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function ApplyModal({
  evaluation,
  onClose,
}: {
  evaluation: GradeEvaluation | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ effectiveDate: Dayjs; decisionNote?: string }>();
  const apply = useApplyGrade();
  return (
    <Modal
      open={evaluation !== null}
      title={GR_LABELS.applyTitle}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={apply.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {evaluation && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{ effectiveDate: dayjs() }}
          onFinish={(v) =>
            apply.mutate(
              {
                id: evaluation.gradeEvaluationId,
                body: {
                  effectiveDate: v.effectiveDate.format(API_DATE),
                  decisionNote: v.decisionNote?.trim() || undefined,
                },
              },
              { onSuccess: onClose },
            )
          }
        >
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`${evaluation.workerName}: ${evaluation.currentGradeCode ?? GR_LABELS.none} → ${evaluation.proposedGradeCode}`}
          />
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item name="decisionNote" label={GR_LABELS.decisionNote}>
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}

function EvaluationsTab() {
  const { hasRole } = usePermission();
  const isLeader = hasRole(ROLES.TeamLeader, ROLES.TeamDeputy);
  const canDecide = hasRole(ROLES.Admin, ROLES.Manager);
  const table = useTableQuery<{ keyword?: string; status?: string }>({});
  const list = useEvaluations(table.params);
  const reject = useRejectGrade();
  const [proposeOpen, setProposeOpen] = useState(false);
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
                <Space size={0}>
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
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 260 }}
        />
        <Select
          allowClear
          placeholder={GR_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 180 }}
        />
        {isLeader && (
          <Button type="primary" onClick={() => setProposeOpen(true)}>
            {GR_LABELS.propose}
          </Button>
        )}
      </Space>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <Table
        rowKey="gradeEvaluationId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
      />
      <ProposeModal open={proposeOpen} onClose={() => setProposeOpen(false)} />
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

function LevelsTab() {
  const levels = useGradeLevels();
  const add = useAddLevel();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm<{
    gradeCode: string;
    gradeName: string;
    monthlyAmount: number;
    effectiveDate: Dayjs;
  }>();

  const columns: TableColumnsType<GradeLevel> = [
    { title: GR_LABELS.gradeCode, dataIndex: 'gradeCode' },
    { title: GR_LABELS.gradeName, dataIndex: 'gradeName' },
    { title: GR_LABELS.amount, dataIndex: 'monthlyAmount', align: 'right', render: formatCurrency },
    { title: GR_LABELS.effectiveDate, dataIndex: 'effectiveDate', render: formatDate },
    {
      title: GR_LABELS.status,
      dataIndex: 'isCurrent',
      render: (v: boolean) => (v ? <Tag color="green">{GR_LABELS.current}</Tag> : null),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Alert
          type="info"
          showIcon
          message="Đổi tiền bậc = thêm một dòng mới với ngày hiệu lực mới, các dòng cũ giữ làm lịch sử."
        />
        <Button type="primary" onClick={() => setOpen(true)}>
          {GR_LABELS.addLevel}
        </Button>
      </Space>
      <QueryError error={levels.error} onRetry={() => void levels.refetch()} />
      <Table
        rowKey="gradeLevelId"
        columns={columns}
        dataSource={levels.data}
        loading={levels.isFetching}
        pagination={false}
      />
      <Modal
        open={open}
        title={GR_LABELS.addLevel}
        okText={MESSAGES.common.save}
        cancelText={MESSAGES.common.cancel}
        confirmLoading={add.isPending}
        onOk={() => form.submit()}
        onCancel={() => setOpen(false)}
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{ effectiveDate: dayjs() }}
          onFinish={(v) =>
            add.mutate(
              {
                gradeCode: v.gradeCode,
                gradeName: v.gradeName.trim(),
                monthlyAmount: v.monthlyAmount,
                effectiveDate: v.effectiveDate.format(API_DATE),
              },
              { onSuccess: () => setOpen(false) },
            )
          }
        >
          <Form.Item name="gradeCode" label={GR_LABELS.gradeCode} rules={required}>
            <Select options={GRADE_OPTIONS} style={{ width: 160 }} />
          </Form.Item>
          <Form.Item
            name="gradeName"
            label={GR_LABELS.gradeName}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
          >
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="monthlyAmount" label={GR_LABELS.amount} rules={required}>
            <InputNumber min={0} step={50000} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

/** UC15 / UC16 / UC50: tổ trưởng đề xuất bậc; Admin / Quản đốc quyết định; Admin đặt mức tiền bậc. */
export function GradePage() {
  const { hasRole } = usePermission();
  return (
    <Card title={GR_LABELS.title}>
      <Tabs
        items={[
          { key: 'evaluations', label: GR_LABELS.evaluationsTab, children: <EvaluationsTab /> },
          ...(hasRole(ROLES.Admin)
            ? [{ key: 'levels', label: GR_LABELS.levelsTab, children: <LevelsTab /> }]
            : []),
        ]}
      />
    </Card>
  );
}
