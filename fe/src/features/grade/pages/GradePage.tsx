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
  Popconfirm,
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
import { EVAL_STATUS, EVAL_STATUS_LABEL, GR_LABELS } from '../constants';
import {
  useAddLevel,
  useCreateGradeCode,
  useDeleteGradeCode,
  useDeleteLevel,
  useGradeCodes,
  useGradeOptions,
  useUpdateGradeCode,
  useUpdateLevel,
  useAllGradeWorkers,
  useApplyGrade,
  useEvaluations,
  useGradeLevels,
  useGradeWorkers,
  useProposeGrade,
  useRejectGrade,
  useSetGrade,
} from '../hooks/useGrade';
import type { GradeCode, GradeEvaluation, GradeLevel } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

const API_DATE = 'YYYY-MM-DD';
const required = [{ required: true, message: MESSAGES.common.required }];
const STATUS_OPTIONS = Object.entries(EVAL_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

function ProposeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<{ workerId: number; proposedGradeCode: string; reason: string }>();
  const workers = useGradeWorkers(open);
  const gradeOptions = useGradeOptions();
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
          <Select options={gradeOptions.options} loading={gradeOptions.isLoading} style={{ width: 200 }} />
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

function SetGradeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const gradeOptions = useGradeOptions();
  const [form] = Form.useForm<{ teamKey: string; workerId: number; gradeCode: string; reason: string; effectiveDate: Dayjs }>();
  const workers = useAllGradeWorkers(open);
  const teamKey = Form.useWatch('teamKey', form);
  const workerId = Form.useWatch('workerId', form);
  const all = workers.data ?? [];
  const teamOptions = [
    ...new Map(all.map((w) => [String(w.teamId ?? 'none'), w.teamName ?? GR_LABELS.noTeam])).entries(),
  ].map(([value, label]) => ({ value, label }));
  const inTeam = all.filter((w) => String(w.teamId ?? 'none') === teamKey);
  const selected = all.find((w) => w.workerId === workerId);
  const setGrade = useSetGrade();
  return (
    <Modal
      open={open}
      title={GR_LABELS.setDirect}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={setGrade.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon style={{ marginBottom: 12 }} message={GR_LABELS.setDirectHint} />
      <Form
        form={form}
        layout="vertical"
        disabled={setGrade.isPending}
        initialValues={{ effectiveDate: dayjs() }}
        onFinish={(v) =>
          setGrade.mutate(
            {
              workerId: v.workerId,
              gradeCode: v.gradeCode,
              reason: v.reason.trim(),
              effectiveDate: v.effectiveDate.format(API_DATE),
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item name="teamKey" label={GR_LABELS.team} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={GR_LABELS.selectTeam}
            loading={workers.isLoading}
            options={teamOptions}
            onChange={() => form.setFieldValue('workerId', undefined)}
          />
        </Form.Item>
        <Form.Item name="workerId" label={GR_LABELS.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            disabled={!teamKey}
            placeholder={teamKey ? GR_LABELS.selectWorker : GR_LABELS.selectTeamFirst}
            options={inTeam.map((w) => ({
              value: w.workerId,
              label: `${w.workerCode} - ${w.fullName} · ${GR_LABELS.currentGrade}: ${w.currentGradeCode ?? GR_LABELS.none}${w.hasPending ? ` (${GR_LABELS.pending})` : ''}`,
            }))}
          />
        </Form.Item>
        {selected && (
          <Form.Item label={GR_LABELS.currentGrade}>
            <Input disabled value={selected.currentGradeCode ?? GR_LABELS.none} style={{ width: 160 }} />
          </Form.Item>
        )}
        <Form.Item name="gradeCode" label={GR_LABELS.newGrade} rules={required}>
          <Select options={gradeOptions.options} loading={gradeOptions.isLoading} style={{ width: 200 }} />
        </Form.Item>
        <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
          <DatePicker format="DD/MM/YYYY" />
        </Form.Item>
        <Form.Item
          name="reason"
          label={GR_LABELS.setReason}
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
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 260 }}
        />
        <FilterSelect value={table.filters.status}
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
        {canDecide && (
          <Button type="primary" onClick={() => setSetOpen(true)}>
            {GR_LABELS.setDirect}
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

function LevelsTab() {
  const gradeOptions = useGradeOptions();
  const levels = useGradeLevels();
  const updateLevel = useUpdateLevel();
  const deleteLevel = useDeleteLevel();
  const [editing, setEditing] = useState<GradeLevel | null>(null);
  const [editForm] = Form.useForm<{ gradeName: string; monthlyAmount: number; effectiveDate: Dayjs }>();
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
    {
      title: MESSAGES.common.actions,
      key: 'action',
      // Chỉ mức chưa hiệu lực mới sửa / xóa được (mức đã hiệu lực là lịch sử lương).
      render: (_, row) =>
        dayjs(row.effectiveDate).isAfter(dayjs(), 'day') ? (
          <Space size={0}>
            <Button
              type="link"
              onClick={() => {
                setEditing(row);
                editForm.setFieldsValue({ gradeName: row.gradeName, monthlyAmount: row.monthlyAmount, effectiveDate: dayjs(row.effectiveDate) });
              }}
            >
              {MESSAGES.common.edit}
            </Button>
            <Popconfirm title={GR_LABELS.deleteLevelConfirm} onConfirm={() => deleteLevel.mutate(row.gradeLevelId)}>
              <Button type="link" danger>
                {MESSAGES.common.delete}
              </Button>
            </Popconfirm>
          </Space>
        ) : null,
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
        open={editing !== null}
        title={GR_LABELS.editLevel}
        okText={MESSAGES.common.save}
        cancelText={MESSAGES.common.cancel}
        confirmLoading={updateLevel.isPending}
        onOk={() => editForm.submit()}
        onCancel={() => setEditing(null)}
        destroyOnHidden
      >
        <Form
          form={editForm}
          layout="vertical"
          onFinish={(v) =>
            editing &&
            updateLevel.mutate(
              {
                id: editing.gradeLevelId,
                body: { gradeName: v.gradeName.trim(), monthlyAmount: v.monthlyAmount, effectiveDate: v.effectiveDate.format(API_DATE) },
              },
              { onSuccess: () => setEditing(null) },
            )
          }
        >
          <Form.Item name="gradeName" label={GR_LABELS.gradeName} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}>
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="monthlyAmount" label={GR_LABELS.amount} rules={required}>
            <InputNumber min={0} step={50000} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
            <DatePicker format="DD/MM/YYYY" disabledDate={(d) => !d.isAfter(dayjs(), 'day')} />
          </Form.Item>
        </Form>
      </Modal>
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
            <Select options={gradeOptions.options} loading={gradeOptions.isLoading} style={{ width: 200 }} />
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

/** Admin: danh mục bậc thợ (thêm, sửa tên / thứ tự, ngừng dùng, xóa bậc chưa ai dùng). */
function CodesTab() {
  const codes = useGradeCodes();
  const create = useCreateGradeCode();
  const update = useUpdateGradeCode();
  const remove = useDeleteGradeCode();
  const [editing, setEditing] = useState<GradeCode | 'new' | null>(null);
  const [form] = Form.useForm<{ gradeCode: string; gradeName: string; sortOrder: number }>();
  const isNew = editing === 'new';
  const current = editing !== null && editing !== 'new' ? editing : null;

  const open = (row: GradeCode | 'new') => {
    setEditing(row);
    form.resetFields();
    form.setFieldsValue(row === 'new' ? { sortOrder: (codes.data?.length ?? 0) + 1 } : { gradeCode: row.gradeCode, gradeName: row.gradeName, sortOrder: row.sortOrder });
  };

  const columns: TableColumnsType<GradeCode> = [
    { title: GR_LABELS.gradeCode, dataIndex: 'gradeCode' },
    { title: GR_LABELS.gradeName, dataIndex: 'gradeName' },
    { title: GR_LABELS.sortOrder, dataIndex: 'sortOrder', align: 'right' },
    {
      title: GR_LABELS.status,
      dataIndex: 'status',
      render: (v: GradeCode['status']) => <Tag color={v === 'Active' ? 'green' : 'default'}>{v === 'Active' ? GR_LABELS.active : GR_LABELS.inactive}</Tag>,
    },
    { title: GR_LABELS.inUse, dataIndex: 'inUse', render: (v: boolean) => (v ? <Tag color="blue">{GR_LABELS.inUse}</Tag> : '—') },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0}>
          <Button type="link" onClick={() => open(row)}>
            {MESSAGES.common.edit}
          </Button>
          <Button
            type="link"
            onClick={() =>
              update.mutate({
                code: row.gradeCode,
                body: { gradeName: row.gradeName, sortOrder: row.sortOrder, status: row.status === 'Active' ? 'Inactive' : 'Active' },
              })
            }
          >
            {row.status === 'Active' ? GR_LABELS.deactivate : GR_LABELS.reactivate}
          </Button>
          {!row.inUse && (
            <Popconfirm title={GR_LABELS.deleteConfirm} onConfirm={() => remove.mutate(row.gradeCode)}>
              <Button type="link" danger>
                {MESSAGES.common.delete}
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <Alert type="info" showIcon message={GR_LABELS.codeHint} />
        <Button type="primary" onClick={() => open('new')}>
          {GR_LABELS.addCode}
        </Button>
      </Space>
      <QueryError error={codes.error} onRetry={() => void codes.refetch()} />
      <Table rowKey="gradeCode" columns={columns} dataSource={codes.data} loading={codes.isFetching} pagination={false} />
      <Modal
        open={editing !== null}
        title={isNew ? GR_LABELS.addCode : GR_LABELS.editCode}
        okText={MESSAGES.common.save}
        cancelText={MESSAGES.common.cancel}
        confirmLoading={create.isPending || update.isPending}
        onOk={() => form.submit()}
        onCancel={() => setEditing(null)}
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(v) => {
            const done = { onSuccess: () => setEditing(null) };
            if (isNew) create.mutate({ gradeCode: v.gradeCode.trim().toUpperCase(), gradeName: v.gradeName.trim(), sortOrder: v.sortOrder }, done);
            else if (current)
              update.mutate({ code: current.gradeCode, body: { gradeName: v.gradeName.trim(), sortOrder: v.sortOrder, status: current.status } }, done);
          }}
        >
          <Form.Item
            name="gradeCode"
            label={GR_LABELS.codeCode}
            rules={[{ required: true, message: MESSAGES.common.required }, { pattern: /^[A-Za-z0-9]{1,5}$/, message: 'Chỉ gồm chữ và số, tối đa 5 ký tự.' }]}
          >
            <Input maxLength={5} disabled={!isNew} style={{ width: 160 }} />
          </Form.Item>
          <Form.Item name="gradeName" label={GR_LABELS.gradeName} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}>
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="sortOrder" label={GR_LABELS.sortOrder} rules={required}>
            <InputNumber min={0} precision={0} style={{ width: 120 }} />
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
            ? [
                { key: 'codes', label: GR_LABELS.codesTab, children: <CodesTab /> },
                { key: 'levels', label: GR_LABELS.levelsTab, children: <LevelsTab /> },
              ]
            : []),
        ]}
      />
    </Card>
  );
}
