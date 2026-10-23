import { useMemo, useState } from 'react';
import { Alert, Button, Card, Col, Drawer, InputNumber, Popconfirm, Row, Space, Spin, Statistic, Tag, Typography } from 'antd';
import { LeftOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { formatDate, formatQty } from '@/utils/format';
import { CAL_TEXT, KPI_TEXT } from '../constants';
import { useKpiCalendar, useSaveKpiDailyPlan } from '../hooks/useKpi';
import type { KpiCalendar, KpiCalendarDay, KpiRole } from '../types';
import { evenSplit, monthsOfPeriod, rampUp } from '../utils/dailyPlan';
import { KpiMonthGrid } from './KpiMonthGrid';

type DrawerProps = { kpiTargetId: number | null; onClose: () => void; role?: KpiRole };

const API_DATE = 'YYYY-MM-DD';
const RED = '#cf1322';
const ORANGE = '#d46b08';

/**
 * Lịch KPI theo ngày của một chỉ tiêu: Quản đốc gõ KPI vào từng ô ngày hoặc bấm "Chia tăng dần" / "Chia đều"; trên đầu có
 * Tổng bán thành phẩm (đã cắt − đã ra chuyền) và Số lượng còn lại để đặt KPI. Admin và KPI đã đóng: chỉ xem.
 */
export function KpiCalendarDrawer({ kpiTargetId, onClose, role = 'manager' }: DrawerProps) {
  const calendar = useKpiCalendar(kpiTargetId, role);
  const data = calendar.data;
  return (
    <Drawer
      open={kpiTargetId !== null}
      onClose={onClose}
      width={1080}
      destroyOnHidden
      title={
        data
          ? `${CAL_TEXT.title} · ${data.orderCode} · ${data.productCode} · ${data.sewingTeamName} · ${formatDate(data.startDate)} - ${formatDate(data.endDate)}`
          : CAL_TEXT.title
      }
    >
      {calendar.isLoading && <Spin />}
      <QueryError error={calendar.error} onRetry={() => void calendar.refetch()} />
      {data && <CalendarBody key={data.kpiTargetId} data={data} manager={role === 'manager'} />}
    </Drawer>
  );
}

function CalendarBody({ data, manager }: { data: KpiCalendar; manager: boolean }) {
  const s = data.summary;
  const save = useSaveKpiDailyPlan(data.kpiTargetId);
  // Chỉ lưu các ô bị sửa (ngày → số mới, 0 = bỏ KPI ngày đó); ô nào trở về đúng số đã lưu thì bỏ khỏi danh sách.
  const [edits, setEdits] = useState<Record<string, number>>({});
  const months = useMemo(() => monthsOfPeriod(data.startDate, data.endDate), [data.startDate, data.endDate]);
  const [monthIndex, setMonthIndex] = useState(() => {
    const i = months.indexOf(dayjs(s.today).startOf('month').format(API_DATE));
    return i >= 0 ? i : 0;
  });

  const editable = manager && s.canEdit;
  const byDate = useMemo(() => new Map(data.days.map((d) => [d.workDate, d])), [data.days]);
  const qtyOf = (d: KpiCalendarDay): number => edits[d.workDate] ?? d.targetQty;
  const locked = (d: KpiCalendarDay): boolean => d.workDate < s.today;   // ngày đã qua không sửa

  const setQty = (d: KpiCalendarDay, value: number | null) => {
    const v = Math.max(0, Math.floor(value ?? 0));
    setEdits((prev) => {
      const next = { ...prev };
      if (v === d.targetQty) delete next[d.workDate];
      else next[d.workDate] = v;
      return next;
    });
  };

  // Tính lại ngay theo bản nháp: số đã chia, phần chưa chia và số BTP còn lại để đặt KPI.
  const allocated = data.days.reduce((sum, d) => sum + qtyOf(d), 0);
  const committed = data.days.reduce(
    (sum, d) => sum + (d.isToday ? Math.max(0, qtyOf(d) - (d.actualQty ?? 0)) : d.isFuture ? qtyOf(d) : 0),
    0,
  );
  const remaining = s.remainingToSet + s.committedThis - committed;
  const unallocated = s.planTotal - allocated;
  const dirty = Object.keys(edits).length > 0;

  // Phần chỉ tiêu còn lại để chia = cả kỳ − số đã làm ở các ngày đã qua; chia cho các ngày từ hôm nay đến hết kỳ (bỏ Chủ nhật).
  const openDays = data.days.filter((d) => !locked(d));
  const workDays = openDays.filter((d) => dayjs(d.workDate).day() !== 0);
  const doneBefore = data.days.filter(locked).reduce((sum, d) => sum + (d.actualQty ?? 0), 0);
  const toSpread = Math.max(0, s.planTotal - doneBefore);
  const applySplit = (split: (total: number, count: number) => number[]) => {
    const qty = split(toSpread, workDays.length);
    const next: Record<string, number> = {};
    openDays.forEach((d) => {
      const i = workDays.indexOf(d);
      const v = i >= 0 ? (qty[i] ?? 0) : 0;
      if (v !== d.targetQty) next[d.workDate] = v;
    });
    setEdits(next);
  };
  const clearOpen = () => {
    const next: Record<string, number> = {};
    openDays.forEach((d) => {
      if (d.targetQty !== 0) next[d.workDate] = 0;
    });
    setEdits(next);
  };

  const submit = () =>
    save.mutate(
      { dailyTargets: data.days.filter((d) => qtyOf(d) > 0).map((d) => ({ workDate: d.workDate, targetQty: qtyOf(d) })) },
      { onSuccess: () => setEdits({}) },
    );

  const month = months[monthIndex] ?? data.startDate;

  const renderDay = (date: string) => {
    const d = byDate.get(date);
    if (!d) return null;
    const qty = qtyOf(d);
    const changed = d.workDate in edits;
    const bad = d.btpRemaining < 0 || d.lowBtp;
    return (
      <>
        {editable && !locked(d) ? (
          <InputNumber
            size="small"
            min={0}
            precision={0}
            controls={false}
            placeholder="KPI"
            value={qty > 0 ? qty : null}
            onChange={(v) => setQty(d, v)}
            status={changed ? 'warning' : undefined}
            style={{ width: '100%' }}
          />
        ) : (
          <Typography.Text strong>{qty > 0 ? formatQty(qty) : '—'}</Typography.Text>
        )}
        {d.actualQty !== null && (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {CAL_TEXT.cell.actual}: <b>{formatQty(d.actualQty)}</b>
          </Typography.Text>
        )}
        <Typography.Text style={{ fontSize: 12, color: bad ? RED : undefined }} type={bad ? undefined : 'secondary'}>
          {CAL_TEXT.cell.btp}: {formatQty(d.btpRemaining)}
          {d.isFuture ? ` (${CAL_TEXT.cell.future.toLowerCase()})` : ''}
        </Typography.Text>
        {d.cutQty > 0 && (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            +{formatQty(d.cutQty)} {CAL_TEXT.cell.cut.toLowerCase()}
          </Typography.Text>
        )}
        <Space size={4} wrap>
          {d.lowBtp && <Tag color="red" style={{ margin: 0 }}>{CAL_TEXT.cell.low}</Tag>}
          {d.behindTarget && <Tag color="orange" style={{ margin: 0 }}>{CAL_TEXT.cell.behind}</Tag>}
        </Space>
      </>
    );
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}><Card size="small"><Statistic title={CAL_TEXT.sum.plan} value={s.planTotal} /></Card></Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic
              title={`${CAL_TEXT.sum.allocated} / ${CAL_TEXT.sum.unallocated.toLowerCase()}`}
              value={`${formatQty(allocated)} / ${formatQty(unallocated)}`}
              valueStyle={unallocated < 0 ? { color: RED } : undefined}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic title={CAL_TEXT.sum.totalBtp} value={s.totalBtp} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>{CAL_TEXT.sum.totalBtpHint(s.cutTotal, s.outTotal)}</Typography.Text>
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic
              title={CAL_TEXT.sum.remaining}
              value={remaining}
              valueStyle={{ color: remaining < 0 ? RED : '#389e0d' }}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {`${CAL_TEXT.sum.committed}: ${formatQty(committed + s.committedAll - s.committedThis)}`}
            </Typography.Text>
          </Card>
        </Col>
      </Row>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>{CAL_TEXT.sum.remainingHint}</Typography.Text>

      {unallocated < 0 && <Alert type="error" showIcon message={CAL_TEXT.overPlan} />}
      {remaining < 0 && <Alert type="warning" showIcon message={CAL_TEXT.overBtp} />}
      {!editable && <Alert type="info" showIcon message={CAL_TEXT.readOnly} />}

      {editable && (
        <Space wrap>
          <Button onClick={() => applySplit(rampUp)} disabled={workDays.length === 0}>{CAL_TEXT.tool.ramp}</Button>
          <Button onClick={() => applySplit(evenSplit)} disabled={workDays.length === 0}>{CAL_TEXT.tool.even}</Button>
          <Popconfirm title={CAL_TEXT.tool.clear + '?'} onConfirm={clearOpen}>
            <Button danger>{CAL_TEXT.tool.clear}</Button>
          </Popconfirm>
          <Button onClick={() => setEdits({})} disabled={!dirty}>{CAL_TEXT.tool.reset}</Button>
          <Button type="primary" onClick={submit} loading={save.isPending} disabled={!dirty || unallocated < 0}>
            {CAL_TEXT.tool.save}
          </Button>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>{CAL_TEXT.tool.rampHint}</Typography.Text>
        </Space>
      )}

      {months.length > 1 && (
        <Space>
          <Button icon={<LeftOutlined />} onClick={() => setMonthIndex((i) => Math.max(0, i - 1))} disabled={monthIndex === 0} aria-label={CAL_TEXT.prevMonth} />
          <Button icon={<RightOutlined />} onClick={() => setMonthIndex((i) => Math.min(months.length - 1, i + 1))} disabled={monthIndex >= months.length - 1} aria-label={CAL_TEXT.nextMonth} />
        </Space>
      )}
      <KpiMonthGrid month={month} renderDay={renderDay} />
      <Space size={16} wrap>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          <span style={{ color: RED }}>●</span> {CAL_TEXT.cell.low}: BTP còn nhỏ hơn KPI ngày kế tiếp
        </Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          <span style={{ color: ORANGE }}>●</span> {CAL_TEXT.cell.behind}: thực tế lũy kế thấp hơn KPI lũy kế
        </Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>{CAL_TEXT.pastLocked}</Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>{KPI_TEXT.statusLabel[data.status].text}</Typography.Text>
      </Space>
    </Space>
  );
}
