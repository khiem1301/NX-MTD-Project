import { useState } from 'react';
import { DeleteOutlined, DollarOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Popover,
  Select,
  Space,
  Tabs,
  Tag,
  Typography,
  type TableColumnsType,
  type InputNumberProps,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { API_DATE, formatCurrency, formatDate } from '@/utils/format';
import { EVAL_STATUS, EVAL_STATUS_LABEL, GR_LABELS } from '../constants';
import {
  useAddLevel,
  useAllGradeWorkers,
  useApplyGrade,
  useCreateGradeCode,
  useDeleteGradeCode,
  useDeleteLevel,
  useEvaluations,
  useGradeCodes,
  useGradeLevels,
  useGradeOptions,
  useGradeWorkers,
  useProposeGrade,
  useRejectGrade,
  useSetGrade,
  useUpdateGradeCode,
  useUpdateLevel,
} from '../hooks/useGrade';
import type { GradeCode, GradeEvaluation, GradeLevel } from '../types';
import { levelsAround } from '../utils';
import { normalizeText } from '@/utils/text';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import { DetailLink } from '@/components/common/DetailLink';
import { DetailModal } from '@/components/common/DetailModal';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';

const required = [{ required: true, message: MESSAGES.common.required }];
const STATUS_OPTIONS = Object.entries(EVAL_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

function ProposeModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const gradeOptions = useGradeOptions();
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
          <Select
            options={gradeOptions.options}
            loading={gradeOptions.isLoading}
            style={{ width: 200 }}
          />
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
  const [form] = Form.useForm<{
    teamKey: string;
    workerId: number;
    gradeCode: string;
    reason: string;
    effectiveDate: Dayjs;
  }>();
  const workers = useAllGradeWorkers(open);
  const teamKey = Form.useWatch('teamKey', form);
  const workerId = Form.useWatch('workerId', form);
  const all = workers.data ?? [];
  const teamOptions = [
    ...new Map(
      all.map((w) => [String(w.teamId ?? 'none'), w.teamName ?? GR_LABELS.noTeam]),
    ).entries(),
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
            <Input
              disabled
              value={selected.currentGradeCode ?? GR_LABELS.none}
              style={{ width: 160 }}
            />
          </Form.Item>
        )}
        <Form.Item name="gradeCode" label={GR_LABELS.newGrade} rules={required}>
          <Select
            options={gradeOptions.options}
            loading={gradeOptions.isLoading}
            style={{ width: 200 }}
          />
        </Form.Item>
        <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
          <AppDatePicker />
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
            <AppDatePicker />
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

const moneyFormatter = (v: number | string | undefined) =>
  `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
const moneyParser = (v: string | undefined) => Number((v ?? '').replace(/\./g, ''));

const currentLevel = (all: GradeLevel[], code: string) =>
  all.find((l) => l.gradeCode === code && l.isCurrent);
/** Các mức đã hẹn ngày tương lai của một bậc, sớm trước: để Admin biết giá sắp áp dụng. */
const upcomingLevels = (all: GradeLevel[], code: string) =>
  all
    .filter(
      (l) => l.gradeCode === code && !l.isCurrent && dayjs(l.effectiveDate).isAfter(dayjs(), 'day'),
    )
    .sort((x, y) => x.effectiveDate.localeCompare(y.effectiveDate));

/** Ô nhập tiền: phải chuyển tiếp props vì Form.Item truyền value / onChange vào phần tử con trực tiếp. */
const MoneyInput = (props: InputNumberProps) => (
  <InputNumber
    min={0}
    step={50000}
    suffix="đ"
    formatter={moneyFormatter}
    parser={moneyParser}
    style={{ width: '100%' }}
    {...props}
  />
);

/** Đổi tiền một bậc: đặt mức mới từ một ngày hiệu lực, kể cả ngày trong tương lai; chọn trùng ngày một mức đã hẹn thì thay mức đó. */
export function ChangeAmountModal({
  code,
  rows,
  onClose,
}: {
  code: GradeCode | null;
  rows: GradeLevel[];
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ monthlyAmount: number; effectiveDate: Dayjs }>();
  const add = useAddLevel();
  const update = useUpdateLevel();
  const levels = rows.filter((l) => l.gradeCode === code?.gradeCode);
  const current = code ? currentLevel(rows, code.gradeCode) : undefined;
  // Chỉ cần sau ngày hiệu lực của mức đang áp dụng; các mức đã hẹn phía sau vẫn đặt thêm hoặc thay được.
  const earliest = current ? dayjs(current.effectiveDate).add(1, 'day') : null;
  const defaultDate = earliest && earliest.isAfter(dayjs(), 'day') ? earliest : dayjs();
  const amount = Form.useWatch('monthlyAmount', form);
  const date = Form.useWatch('effectiveDate', form);
  const around = levelsAround(levels, (date ?? defaultDate).format(API_DATE));
  const base = around.before;
  const diff = base && amount !== null && amount !== undefined ? amount - base.monthlyAmount : null;
  return (
    <Modal
      open={code !== null}
      title={
        code
          ? `${current ? GR_LABELS.changeAmount : GR_LABELS.addLevel} - ${code.gradeCode} · ${code.gradeName}`
          : ''
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.md}
      confirmLoading={add.isPending || update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {code && (
        <Form
          form={form}
          layout="vertical"
          disabled={add.isPending || update.isPending}
          initialValues={{ monthlyAmount: current?.monthlyAmount, effectiveDate: defaultDate }}
          onFinish={(v) => {
            const effectiveDate = v.effectiveDate.format(API_DATE);
            const replaced = levelsAround(levels, effectiveDate).sameDay;
            if (replaced) {
              update.mutate(
                {
                  id: replaced.gradeLevelId,
                  body: { monthlyAmount: v.monthlyAmount, effectiveDate },
                },
                { onSuccess: onClose },
              );
              return;
            }
            add.mutate(
              { gradeCode: code.gradeCode, monthlyAmount: v.monthlyAmount, effectiveDate },
              { onSuccess: onClose },
            );
          }}
        >
          {current && (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message={`${GR_LABELS.currentAmount}: ${formatCurrency(current.monthlyAmount)} (${GR_LABELS.effectiveDate.toLowerCase()} ${formatDate(current.effectiveDate)})`}
            />
          )}
          <Typography.Text strong>{GR_LABELS.levelHistory}</Typography.Text>
          <DataTable
            rowKey="gradeLevelId"
            columns={levelColumns(levels)}
            dataSource={levels}
            pagination={false}
            scroll={{ x: 'max-content', y: 180 }}
            style={{ margin: '8px 0 16px' }}
          />
          <Form.Item
            name="monthlyAmount"
            label={GR_LABELS.newAmount}
            dependencies={['effectiveDate']}
            rules={[
              ...required,
              ({ getFieldValue }) => ({
                validator: async (_rule, value?: number) => {
                  const d = getFieldValue('effectiveDate') as Dayjs | undefined;
                  const ref = d ? levelsAround(levels, d.format(API_DATE)).inEffect : current;
                  if (ref && value === ref.monthlyAmount) throw new Error(GR_LABELS.sameAmount);
                },
              }),
            ]}
          >
            <MoneyInput />
          </Form.Item>
          {diff !== null && diff !== 0 && (
            <div style={{ marginTop: -16, marginBottom: 16 }}>
              <Tag color={diff > 0 ? 'green' : 'red'}>
                {`${diff > 0 ? '+' : '-'}${formatCurrency(Math.abs(diff))}${
                  base && base.monthlyAmount > 0
                    ? ` (${diff > 0 ? '+' : '-'}${Math.round((Math.abs(diff) / base.monthlyAmount) * 1000) / 10}%)`
                    : ''
                }`}
              </Tag>
            </div>
          )}
          <Form.Item
            name="effectiveDate"
            label={GR_LABELS.effectiveDate}
            rules={required}
            extra={
              around.sameDay
                ? `${GR_LABELS.replaceHint}: ${formatCurrency(around.sameDay.monthlyAmount)}`
                : GR_LABELS.effectiveHint
            }
          >
            <AppDatePicker
              allowClear={false}
              disabledDate={(d) => earliest !== null && d.isBefore(earliest, 'day')}
            />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}

/** Sửa mức tiền bậc chưa hiệu lực (mức đã hiệu lực là lịch sử lương, không sửa). */
function EditLevelModal({ level, onClose }: { level: GradeLevel | null; onClose: () => void }) {
  const [form] = Form.useForm<{ monthlyAmount: number; effectiveDate: Dayjs }>();
  const update = useUpdateLevel();
  return (
    <Modal
      open={level !== null}
      title={GR_LABELS.editLevel}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.sm}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {level && (
        <Form
          form={form}
          layout="vertical"
          disabled={update.isPending}
          initialValues={{
            monthlyAmount: level.monthlyAmount,
            effectiveDate: dayjs(level.effectiveDate),
          }}
          onFinish={(v) =>
            update.mutate(
              {
                id: level.gradeLevelId,
                body: {
                  monthlyAmount: v.monthlyAmount,
                  effectiveDate: v.effectiveDate.format(API_DATE),
                },
              },
              { onSuccess: onClose },
            )
          }
        >
          <Form.Item name="monthlyAmount" label={GR_LABELS.newAmount} rules={required}>
            <MoneyInput />
          </Form.Item>
          <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
            <AppDatePicker allowClear={false} disabledDate={(d) => !d.isAfter(dayjs(), 'day')} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}

/** Cột dùng chung của bảng lịch sử mức tiền: ngày hiệu lực, tiền, chênh lệch so với mức trước, trạng thái. */
const levelColumns = (list: GradeLevel[]): TableColumnsType<GradeLevel> => {
  const previousOf = (row: GradeLevel) =>
    list
      .filter((l) => l.effectiveDate < row.effectiveDate)
      .sort((x, y) => y.effectiveDate.localeCompare(x.effectiveDate))[0];
  return [
    { title: GR_LABELS.effectiveDate, dataIndex: 'effectiveDate', render: formatDate },
    { title: GR_LABELS.amount, dataIndex: 'monthlyAmount', align: 'right', render: formatCurrency },
    {
      title: GR_LABELS.diffFromPrev,
      key: 'diff',
      align: 'right',
      render: (_, row) => {
        const prev = previousOf(row);
        const d = prev ? row.monthlyAmount - prev.monthlyAmount : 0;
        if (d === 0) return '—';
        return (
          <Tag
            color={d > 0 ? 'green' : 'red'}
          >{`${d > 0 ? '+' : '-'}${formatCurrency(Math.abs(d))}`}</Tag>
        );
      },
    },
    {
      title: GR_LABELS.status,
      dataIndex: 'isCurrent',
      render: (v: boolean, row) =>
        v ? (
          <Tag color="green">{GR_LABELS.current}</Tag>
        ) : dayjs(row.effectiveDate).isAfter(dayjs(), 'day') ? (
          <Tag color="blue">{GR_LABELS.upcoming}</Tag>
        ) : (
          <Tag>{GR_LABELS.expired}</Tag>
        ),
    },
  ];
};

/** Lịch sử mức tiền của một bậc; mức chưa hiệu lực sửa / xóa được. */
function LevelHistoryModal({
  code,
  rows,
  onClose,
}: {
  code: GradeCode | null;
  rows: GradeLevel[];
  onClose: () => void;
}) {
  const deleteLevel = useDeleteLevel();
  const [editing, setEditing] = useState<GradeLevel | null>(null);
  const list = rows.filter((l) => l.gradeCode === code?.gradeCode);
  const columns: TableColumnsType<GradeLevel> = [
    ...levelColumns(list),
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) =>
        dayjs(row.effectiveDate).isAfter(dayjs(), 'day') ? (
          <Space size={0} wrap>
            <Button type="link" icon={<EditOutlined />} onClick={() => setEditing(row)}>
              {MESSAGES.common.edit}
            </Button>
            <Popconfirm
              title={GR_LABELS.deleteLevelConfirm}
              onConfirm={() => deleteLevel.mutate(row.gradeLevelId)}
            >
              <Button type="link" danger icon={<DeleteOutlined />}>
                {MESSAGES.common.delete}
              </Button>
            </Popconfirm>
          </Space>
        ) : null,
    },
  ];

  return (
    <DetailModal
      open={code !== null}
      onClose={onClose}
      size="md"
      title={code ? `${GR_LABELS.levelHistory} - ${code.gradeCode} · ${code.gradeName}` : undefined}
    >
      <Alert type="info" showIcon style={{ marginBottom: 16 }} message={GR_LABELS.futureOnlyHint} />
      <DataTable rowKey="gradeLevelId" columns={columns} dataSource={list} pagination={false} />
      <EditLevelModal level={editing} onClose={() => setEditing(null)} />
    </DetailModal>
  );
}

type CodeForm = {
  gradeCode: string;
  gradeName: string;
  monthlyAmount?: number;
  effectiveDate?: Dayjs;
};

/** Admin: bậc thợ và mức tiền bậc trong một bảng (mỗi dòng một bậc). Thêm bậc kèm luôn mức tiền đầu tiên. */
function GradeSetupTab() {
  const codes = useGradeCodes();
  const levels = useGradeLevels();
  const create = useCreateGradeCode();
  const addLevel = useAddLevel();
  const update = useUpdateGradeCode();
  const remove = useDeleteGradeCode();
  const [editing, setEditing] = useState<GradeCode | 'new' | null>(null);
  const [changing, setChanging] = useState<GradeCode | null>(null);
  const [viewing, setViewing] = useState<GradeCode | null>(null);
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState<GradeCode['status']>();
  const [form] = Form.useForm<CodeForm>();
  const all = levels.data ?? [];
  const needle = normalizeText(keyword.trim());
  const visible = (codes.data ?? []).filter(
    (c) =>
      (statusFilter === undefined || c.status === statusFilter) &&
      (needle === '' || normalizeText(`${c.gradeCode} ${c.gradeName}`).includes(needle)),
  );
  const isNew = editing === 'new';
  const current = editing !== null && editing !== 'new' ? editing : null;

  const open = (row: GradeCode | 'new') => {
    setEditing(row);
    form.resetFields();
    form.setFieldsValue(
      row === 'new'
        ? { effectiveDate: dayjs() }
        : { gradeCode: row.gradeCode, gradeName: row.gradeName },
    );
  };

  const submit = (v: CodeForm) => {
    const done = { onSuccess: () => setEditing(null) };
    if (current) {
      update.mutate(
        {
          code: current.gradeCode,
          body: { gradeName: v.gradeName.trim(), status: current.status },
        },
        done,
      );
      return;
    }
    const gradeCode = v.gradeCode.trim().toUpperCase();
    const gradeName = v.gradeName.trim();
    // Tạo bậc rồi đặt luôn mức tiền đầu tiên; nếu bước tiền lỗi thì bậc vẫn còn, dùng "Đổi tiền" để đặt lại.
    create.mutate(
      { gradeCode, gradeName },
      {
        onSuccess: () =>
          addLevel.mutate(
            {
              gradeCode,
              monthlyAmount: v.monthlyAmount ?? 0,
              effectiveDate: (v.effectiveDate ?? dayjs()).format(API_DATE),
            },
            { onSettled: () => setEditing(null) },
          ),
      },
    );
  };

  const columns: TableColumnsType<GradeCode> = [
    {
      title: GR_LABELS.gradeCode,
      dataIndex: 'gradeCode',
      width: 100,
      render: (v: string, row) => <DetailLink onClick={() => setViewing(row)}>{v}</DetailLink>,
    },
    { title: GR_LABELS.gradeName, dataIndex: 'gradeName' },
    {
      title: GR_LABELS.amount,
      key: 'amount',
      align: 'right',
      render: (_, row) => {
        const cur = currentLevel(all, row.gradeCode);
        return cur ? formatCurrency(cur.monthlyAmount) : <Tag>{GR_LABELS.none}</Tag>;
      },
    },
    {
      title: GR_LABELS.effectiveDate,
      key: 'effective',
      render: (_, row) => {
        const cur = currentLevel(all, row.gradeCode);
        const next = upcomingLevels(all, row.gradeCode);
        const first = next[0];
        return (
          <Space direction="vertical" size={0}>
            {cur ? formatDate(cur.effectiveDate) : '—'}
            {first && (
              <Space size={4} wrap={false}>
                <Tag color="blue" style={{ marginInlineEnd: 0 }}>
                  {GR_LABELS.upcoming}
                </Tag>
                <span style={{ whiteSpace: 'nowrap' }}>
                  {`${formatCurrency(first.monthlyAmount)} · ${formatDate(first.effectiveDate)}`}
                </span>
                {next.length > 1 && (
                  <Popover
                    title={GR_LABELS.upcoming}
                    content={
                      <div style={{ maxHeight: 200, overflowY: 'auto' }}>
                        {next.map((n) => (
                          <div key={n.gradeLevelId} style={{ whiteSpace: 'nowrap' }}>
                            {`${formatDate(n.effectiveDate)} · ${formatCurrency(n.monthlyAmount)}`}
                          </div>
                        ))}
                      </div>
                    }
                  >
                    <Tag
                      style={{ cursor: 'pointer', marginInlineEnd: 0 }}
                    >{`+${next.length - 1}`}</Tag>
                  </Popover>
                )}
              </Space>
            )}
          </Space>
        );
      },
    },
    {
      title: GR_LABELS.status,
      dataIndex: 'status',
      render: (v: GradeCode['status'], row) => (
        <Space size={4} wrap>
          <Tag color={v === 'Active' ? 'green' : 'default'}>
            {v === 'Active' ? GR_LABELS.active : GR_LABELS.inactive}
          </Tag>
          {row.inUse && <Tag color="blue">{GR_LABELS.inUse}</Tag>}
        </Space>
      ),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0} wrap>
          <Button type="link" icon={<DollarOutlined />} onClick={() => setChanging(row)}>
            {GR_LABELS.changeAmount}
          </Button>
          <Button type="link" icon={<EditOutlined />} onClick={() => open(row)}>
            {MESSAGES.common.edit}
          </Button>
          <Button
            type="link"
            onClick={() =>
              update.mutate({
                code: row.gradeCode,
                body: {
                  gradeName: row.gradeName,
                  status: row.status === 'Active' ? 'Inactive' : 'Active',
                },
              })
            }
          >
            {row.status === 'Active' ? GR_LABELS.deactivate : GR_LABELS.reactivate}
          </Button>
          {!row.inUse && (
            <Popconfirm
              title={GR_LABELS.deleteConfirm}
              onConfirm={() => remove.mutate(row.gradeCode)}
            >
              <Button type="link" danger icon={<DeleteOutlined />}>
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
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={GR_LABELS.searchGrade}
              onSearch={(k) => setKeyword(k)}
              style={{ width: 260 }}
            />
            <FilterSelect
              value={statusFilter}
              placeholder={GR_LABELS.status}
              options={[
                { value: 'Active', label: GR_LABELS.active },
                { value: 'Inactive', label: GR_LABELS.inactive },
              ]}
              onChange={(v?: GradeCode['status']) => setStatusFilter(v)}
              style={{ width: 160 }}
            />
          </>
        }
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => open('new')}>
            {GR_LABELS.addCode}
          </Button>
        }
      />
      <QueryError error={codes.error ?? levels.error} onRetry={() => void codes.refetch()} />
      <DataTable
        rowKey="gradeCode"
        columns={columns}
        dataSource={visible}
        loading={codes.isFetching || levels.isFetching}
        pagination={false}
      />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>
        {GR_LABELS.codeHint}
      </Typography.Paragraph>
      <Modal
        open={editing !== null}
        title={isNew ? GR_LABELS.addCode : GR_LABELS.editCode}
        okText={MESSAGES.common.save}
        cancelText={MESSAGES.common.cancel}
        width={MODAL_WIDTH.sm}
        confirmLoading={create.isPending || addLevel.isPending || update.isPending}
        onOk={() => form.submit()}
        onCancel={() => setEditing(null)}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={submit}>
          <Form.Item
            name="gradeCode"
            label={GR_LABELS.codeCode}
            rules={[
              { required: true, message: MESSAGES.common.required },
              { pattern: /^[A-Za-z0-9]{1,5}$/, message: 'Chỉ gồm chữ và số, tối đa 5 ký tự.' },
            ]}
          >
            <Input maxLength={5} disabled={!isNew} style={{ width: 160 }} />
          </Form.Item>
          <Form.Item
            name="gradeName"
            label={GR_LABELS.gradeName}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
          >
            <Input maxLength={50} />
          </Form.Item>
          {isNew && (
            <>
              <Form.Item name="monthlyAmount" label={GR_LABELS.amount} rules={required}>
                <MoneyInput />
              </Form.Item>
              <Form.Item name="effectiveDate" label={GR_LABELS.effectiveDate} rules={required}>
                <AppDatePicker allowClear={false} />
              </Form.Item>
            </>
          )}
        </Form>
      </Modal>
      <ChangeAmountModal code={changing} rows={all} onClose={() => setChanging(null)} />
      <LevelHistoryModal code={viewing} rows={all} onClose={() => setViewing(null)} />
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
            ? [{ key: 'setup', label: GR_LABELS.levelsTab, children: <GradeSetupTab /> }]
            : []),
        ]}
      />
    </Card>
  );
}
