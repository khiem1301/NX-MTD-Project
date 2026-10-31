import { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Grid,
  Input,
  InputNumber,
  Select,
  Space,
  Tag,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { useAuth } from '@/hooks/useAuth';
import { useTeamLookup, useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, formatCurrency, formatDateTime, formatQty, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT, SHIFT_OPTIONS } from '../constants';
import { useAssignableOperations, useOutputGrid, useSaveOutput } from '../hooks/useProduction';
import type { OutputGridRow, OutputLineInput, OutputRole, ShiftCode } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';

const text = PRODUCTION_TEXT.output;
const keyOf = (row: Pick<OutputGridRow, 'operationId' | 'workerId'>) =>
  `${row.operationId}-${row.workerId}`;

/**
 * UC55 (KI-04) / UC22 (KI-11) — lưới sản lượng cuối ca: mỗi dòng là một công nhân × công đoạn đã phân công.
 * Tổ trưởng / tổ phó nhập và sửa trong hạn; Quản đốc sửa hộ tổ kể cả sau hạn, bắt buộc ghi lý do.
 * Số lượng 0 ở dòng đã nhập nghĩa là xóa dòng đó.
 */
export function OutputEntryBoard({ role }: { role: OutputRole }) {
  const isManager = role === 'manager';
  // Trên điện thoại (KI-14) lưới hiện dạng thẻ: mỗi công nhân một thẻ, ô nhập số lớn dễ bấm.
  const isMobile = Grid.useBreakpoint().md === false;
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [shift, setShift] = useState<ShiftCode>('Afternoon');
  const [orderId, setOrderId] = useState<number | null>(null);
  const [orderLineId, setOrderLineId] = useState<number | null>(null);
  const [teamId, setTeamId] = useState<number | null>(null);
  const [edits, setEdits] = useState<Record<string, number | null>>({});
  const [reason, setReason] = useState('');
  const [warnings, setWarnings] = useState<string[]>([]);
  // Dòng ngoài phân công tổ trưởng vừa thêm, chưa lưu (GBR-20); lưu xong thì lưới tải lại từ BE.
  const [extraRows, setExtraRows] = useState<OutputGridRow[]>([]);
  const [extraOperationId, setExtraOperationId] = useState<number | undefined>(undefined);
  const [extraWorkerId, setExtraWorkerId] = useState<number | undefined>(undefined);

  const { user } = useAuth();
  const operations = useAssignableOperations(isManager ? null : orderId);
  const members = useTeamMemberLookup(isManager ? null : (user?.teamId ?? null));
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
  const rows = useMemo(() => {
    const gridRows = grid.data?.rows ?? [];
    return [...gridRows, ...extraRows.filter((e) => !gridRows.some((g) => keyOf(g) === keyOf(e)))];
  }, [grid.data, extraRows]);

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
    setExtraRows([]);
  };

  const addExtraRow = () => {
    const operation = operations.data?.find((o) => o.operationId === extraOperationId);
    const worker = members.data?.find((m) => m.employeeId === extraWorkerId);
    if (!operation || !worker) return;
    setExtraRows((prev) => [
      ...prev,
      {
        operationId: operation.operationId,
        operationCode: operation.operationCode,
        operationName: operation.operationName,
        workerId: worker.employeeId,
        workerCode: worker.employeeCode,
        workerName: worker.fullName,
        outputRecordId: null,
        quantity: null,
        unitRate: null,
        rateApproved: true,
        isOutsideAssignment: true,
        capQty: 0,
        reportedQty: 0,
        noAttendanceWarning: false,
        canEdit: true,
        editDeadline: null,
        rowVer: null,
      },
    ]);
    setExtraOperationId(undefined);
    setExtraWorkerId(undefined);
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
          void message.success(
            fill(text.resultTemplate, result.inserted, result.updated, result.deleted),
          );
          setWarnings(result.warnings);
          setEdits({});
          setExtraRows([]);
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
    {
      title: text.columns.operation,
      key: 'op',
      render: (_, r) => `${r.operationCode} - ${r.operationName}`,
    },
    {
      title: text.columns.worker,
      key: 'worker',
      render: (_, r) => `${r.workerCode} - ${r.workerName}`,
    },
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
    {
      title: text.columns.allocated,
      key: 'allocated',
      align: 'right',
      render: (_, r) =>
        r.capQty > 0 ? (
          `${r.reportedQty} / ${r.capQty}`
        ) : (
          <Tag color="red">{text.tags.notAllocated}</Tag>
        ),
    },
    {
      responsive: ['md'],
      title: text.columns.rate,
      dataIndex: 'unitRate',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    {
      responsive: ['md'],
      title: text.columns.deadline,
      dataIndex: 'editDeadline',
      render: (v: string | null) => formatDateTime(v),
    },
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
    {
      title: '',
      key: 'remove',
      render: (_, r) =>
        r.outputRecordId === null && extraRows.some((e) => keyOf(e) === keyOf(r)) ? (
          <Button
            type="link"
            danger
            onClick={() => {
              setExtraRows((prev) => prev.filter((e) => keyOf(e) !== keyOf(r)));
              setEdits((prev) => {
                const { [keyOf(r)]: _removed, ...rest } = prev;
                return rest;
              });
            }}
          >
            {text.removeRow}
          </Button>
        ) : null,
    },
  ];

  return (
    <PageCard
      title={isManager ? text.managerTitle : text.title}
      description={isManager ? text.managerSubtitle : text.subtitle}
    >
      <Space wrap>
        <AppDatePicker
          allowClear={false}
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
          placeholder={
            orderId === null ? PRODUCTION_TEXT.selectFirst : PRODUCTION_TEXT.selectOrderLine
          }
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
        <Alert
          type="warning"
          showIcon
          message={warnings.map((w) => (
            <div key={w}>{w}</div>
          ))}
          closable
          onClose={() => setWarnings([])}
        />
      )}

      {!isManager && query !== null && (
        <Space wrap align="center">
          <Typography.Text type="secondary">{text.addOutside}:</Typography.Text>
          <Select<number>
            showSearch
            optionFilterProp="label"
            style={{ width: 280 }}
            placeholder={PRODUCTION_TEXT.selectOperation}
            loading={operations.isLoading}
            value={extraOperationId}
            options={(operations.data ?? []).map((o) => ({
              value: o.operationId,
              label: `${o.operationCode} - ${o.operationName}`,
            }))}
            onChange={setExtraOperationId}
          />
          <Select<number>
            showSearch
            optionFilterProp="label"
            style={{ width: 240 }}
            placeholder={PRODUCTION_TEXT.selectWorker}
            loading={members.isLoading}
            value={extraWorkerId}
            options={(members.data ?? [])
              .filter((m) => m.role === 'Worker')
              .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }))}
            onChange={setExtraWorkerId}
          />
          <Button
            disabled={extraOperationId === undefined || extraWorkerId === undefined}
            onClick={addExtraRow}
          >
            {text.addRow}
          </Button>
        </Space>
      )}

      {isMobile ? (
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          {rows.length === 0 && (
            <Typography.Text type="secondary">
              {query === null ? PRODUCTION_TEXT.selectOrderLine : text.noRows}
            </Typography.Text>
          )}
          {rows.map((r) => (
            <Card key={keyOf(r)} size="small" loading={grid.isFetching}>
              <Space direction="vertical" size={6} style={{ width: '100%' }}>
                <Typography.Text
                  strong
                >{`${r.operationCode} - ${r.operationName}`}</Typography.Text>
                <Typography.Text type="secondary">{`${r.workerCode} - ${r.workerName}`}</Typography.Text>
                <Space size={4} wrap>
                  {r.isOutsideAssignment && <Tag color="orange">{text.tags.outside}</Tag>}
                  {r.noAttendanceWarning && <Tag color="orange">{text.tags.noAttendance}</Tag>}
                  {!r.rateApproved && <Tag color="red">{text.tags.noRate}</Tag>}
                  {r.quantity !== null && !r.canEdit && <Tag>{text.tags.locked}</Tag>}
                </Space>
                <InputNumber
                  size="large"
                  min={0}
                  precision={0}
                  inputMode="numeric"
                  addonBefore={text.columns.qty}
                  style={{ width: '100%' }}
                  disabled={!isManager && !r.canEdit}
                  value={edits[keyOf(r)] !== undefined ? edits[keyOf(r)] : r.quantity}
                  onChange={(v) => setEdits((prev) => ({ ...prev, [keyOf(r)]: v }))}
                />
                {r.unitRate !== null && (
                  <Typography.Text type="secondary">{`${text.columns.rate}: ${formatCurrency(r.unitRate)}`}</Typography.Text>
                )}
                {r.editDeadline && (
                  <Typography.Text type="secondary">{`${text.columns.deadline}: ${formatDateTime(r.editDeadline)}`}</Typography.Text>
                )}
              </Space>
            </Card>
          ))}
        </Space>
      ) : (
        <DataTable<OutputGridRow>
          rowKey={keyOf}
          size="small"
          columns={columns}
          dataSource={rows}
          loading={grid.isFetching}
          locale={{ emptyText: query === null ? PRODUCTION_TEXT.selectOrderLine : text.noRows }}
          scroll={{ x: 'max-content' }}
          pagination={false}
        />
      )}

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
          <Button
            type="primary"
            loading={save.isPending}
            disabled={changedRows.length === 0}
            onClick={handleSave}
          >
            {`${text.save}${changedRows.length > 0 ? ` (${changedRows.length})` : ''}`}
          </Button>
        </Space>
      )}
    </PageCard>
  );
}
