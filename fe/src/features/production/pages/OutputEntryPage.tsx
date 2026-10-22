import { useMemo, useState } from 'react';
import { Alert, Button, DatePicker, Input, InputNumber, Select, Space, Table, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, formatCurrency, formatDateTime, formatQty, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT, SHIFT_OPTIONS } from '../constants';
import { useOutputGrid, useSaveOutput } from '../hooks/useProduction';
import type { OutputGridRow, OutputLineInput, OutputRole, ShiftCode } from '../types';

const text = PRODUCTION_TEXT.output;
const keyOf = (row: Pick<OutputGridRow, 'operationId' | 'workerId'>) => `${row.operationId}-${row.workerId}`;

/**
 * UC55 (KI-04) / UC22 (KI-11) — lưới sản lượng cuối ca: mỗi dòng là một công nhân × công đoạn đã phân công.
 * Tổ trưởng / tổ phó nhập và sửa trong hạn; Quản đốc sửa hộ tổ kể cả sau hạn, bắt buộc ghi lý do.
 * Số lượng 0 ở dòng đã nhập nghĩa là xóa dòng đó.
 */
function OutputEntryBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [shift, setShift] = useState<ShiftCode>('Afternoon');
  const [orderId, setOrderId] = useState<number | null>(null);
  const [orderLineId, setOrderLineId] = useState<number | null>(null);
  const [teamId, setTeamId] = useState<number | null>(null);
  const [edits, setEdits] = useState<Record<string, number | null>>({});
  const [reason, setReason] = useState('');
  const [warnings, setWarnings] = useState<string[]>([]);

  const lines = useOrderLineLookup(orderId);
  const teams = useTeamLookup();
  const save = useSaveOutput(role);

  const query =
    orderLineId !== null && (!isManager || teamId !== null)
      ? {
          workDate: toApiDate(workDate),
          shiftCode: shift,
          orderLineId,
          sewingTeamId: isManager ? (teamId ?? undefined) : undefined,
        }
      : null;
  const grid = useOutputGrid(role, query);
  const rows = useMemo(() => grid.data?.rows ?? [], [grid.data]);

  const changedRows = useMemo<OutputLineInput[]>(
    () =>
      rows
        .filter((r) => {
          const edit = edits[keyOf(r)];
          return edit !== undefined && edit !== null && edit !== (r.quantity ?? null);
        })
        .map((r) => ({
          operationId: r.operationId,
          workerId: r.workerId,
          quantity: edits[keyOf(r)] as number,
          rowVer: r.rowVer,
        })),
    [rows, edits],
  );

  const resetEdits = () => {
    setEdits({});
    setWarnings([]);
  };

  const handleSave = () => {
    if (!query || changedRows.length === 0) {
      void message.info(text.nothingChanged);
      return;
    }
    if (isManager && reason.trim() === '') {
      void message.error(text.reasonRequired);
      return;
    }
    save.mutate(
      { ...query, reason: reason.trim() || undefined, rows: changedRows },
      {
        onSuccess: (result) => {
          void message.success(fill(text.resultTemplate, result.inserted, result.updated, result.deleted));
          setWarnings(result.warnings);
          setEdits({});
          setReason('');
        },
      },
    );
  };

  const lineOptions = (lines.data ?? []).map((l) => ({
    value: l.orderLineId,
    label: `${l.productCode} - ${l.color} - ${l.sizeCode} (đặt ${formatQty(l.orderedQty)})`,
  }));
  const teamOptions = (teams.data ?? [])
    .filter((t) => t.teamType === 'Sewing')
    .map((t) => ({ value: t.teamId, label: t.teamName }));

  const columns: ColumnsType<OutputGridRow> = [
    { title: text.columns.operation, key: 'op', render: (_, r) => `${r.operationCode} - ${r.operationName}` },
    { title: text.columns.worker, key: 'worker', render: (_, r) => `${r.workerCode} - ${r.workerName}` },
    {
      title: text.columns.qty,
      key: 'qty',
      width: 160,
      render: (_, r) => (
        <InputNumber
          min={0}
          precision={0}
          style={{ width: 120 }}
          disabled={!isManager && !r.canEdit}
          value={edits[keyOf(r)] !== undefined ? edits[keyOf(r)] : r.quantity}
          onChange={(v) => setEdits((prev) => ({ ...prev, [keyOf(r)]: v }))}
        />
      ),
    },
    { title: text.columns.rate, dataIndex: 'unitRate', align: 'right', render: (v: number | null) => (v === null ? '—' : formatCurrency(v)) },
    { title: text.columns.deadline, dataIndex: 'editDeadline', render: (v: string | null) => formatDateTime(v) },
    {
      title: text.columns.warnings,
      key: 'warnings',
      render: (_, r) => (
        <Space size={4} wrap>
          {r.isOutsideAssignment && <Tag color="orange">{text.tags.outside}</Tag>}
          {r.noAttendanceWarning && <Tag color="orange">{text.tags.noAttendance}</Tag>}
          {!r.rateApproved && <Tag color="red">{text.tags.noRate}</Tag>}
          {r.quantity !== null && !r.canEdit && <Tag>{text.tags.locked}</Tag>}
        </Space>
      ),
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
        <DatePicker
          allowClear={false}
          format="DD/MM/YYYY"
          value={workDate}
          onChange={(v) => {
            setWorkDate(v ?? dayjs());
            resetEdits();
          }}
        />
        <Select<ShiftCode>
          style={{ width: 130 }}
          value={shift}
          options={SHIFT_OPTIONS}
          onChange={(v) => {
            setShift(v);
            resetEdits();
          }}
        />
        {isManager && (
          <Select<number>
            style={{ width: 180 }}
            placeholder={PRODUCTION_TEXT.selectTeam}
            value={teamId ?? undefined}
            options={teamOptions}
            loading={teams.isLoading}
            onChange={(v) => {
              setTeamId(v);
              resetEdits();
            }}
          />
        )}
        <OrderSelect
          value={orderId ?? undefined}
          placeholder={PRODUCTION_TEXT.selectOrder}
          onChange={(v) => {
            setOrderId(v ?? null);
            setOrderLineId(null);
            resetEdits();
          }}
        />
        <Select<number>
          style={{ width: 300 }}
          showSearch
          optionFilterProp="label"
          placeholder={orderId === null ? PRODUCTION_TEXT.selectFirst : PRODUCTION_TEXT.selectOrderLine}
          disabled={orderId === null}
          loading={lines.isLoading}
          value={orderLineId ?? undefined}
          options={lineOptions}
          onChange={(v) => {
            setOrderLineId(v);
            resetEdits();
          }}
        />
      </Space>

      <QueryError error={grid.error} onRetry={() => void grid.refetch()} />

      {grid.data && (
        <Typography.Text>
          {text.received}: <strong>{formatQty(grid.data.receivedFromWarehouse)}</strong>
        </Typography.Text>
      )}

      {warnings.length > 0 && (
        <Alert type="warning" showIcon message={warnings.map((w) => <div key={w}>{w}</div>)} closable onClose={() => setWarnings([])} />
      )}

      <Table<OutputGridRow>
        rowKey={keyOf}
        size="middle"
        columns={columns}
        dataSource={rows}
        loading={grid.isFetching}
        locale={{ emptyText: query === null ? PRODUCTION_TEXT.selectOrderLine : text.noRows }}
        scroll={{ x: 'max-content' }}
        pagination={false}
      />

      {rows.length > 0 && (
        <Space direction="vertical" style={{ width: '100%' }}>
          {isManager && (
            <Input.TextArea
              rows={2}
              maxLength={500}
              showCount
              placeholder={text.reason}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          )}
          <Button type="primary" loading={save.isPending} disabled={changedRows.length === 0} onClick={handleSave}>
            {`${text.save}${changedRows.length > 0 ? ` (${changedRows.length})` : ''}`}
          </Button>
        </Space>
      )}
    </Space>
  );
}

/** Tổ trưởng / tổ phó may nhập sản lượng của tổ mình. */
export const OutputEntryPage = () => <OutputEntryBoard role="team-leader" />;

/** Quản đốc sửa sản lượng của tổ, kể cả sau hạn. */
export const ManagerOutputPage = () => <OutputEntryBoard role="manager" />;
