import { Alert, Button, Descriptions, Flex, InputNumber, Space, Tooltip, Typography } from 'antd';
import { InfoCircleOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';
import { fill, formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useSaveKpiDailyPlan } from '../hooks/useKpi';
import { useKpiDailyPlan } from '../hooks/useKpiDailyPlan';
import type { KpiCalendar } from '../types';
import { KpiMonthCalendar } from './KpiMonthCalendar';

type KpiCalendarBodyProps = {
  data: KpiCalendar;
  /** Quản đốc đặt KPI khi chỉ tiêu còn chạy; Admin và chỉ tiêu đã đóng chỉ xem. */
  editable: boolean;
};

const text = KPI_TEXT.calendar;
const SMALL = { fontSize: 12 };
/** Độ dài phần HH:mm của giờ API trả về. */
const TIME_LENGTH = 5;

const hintLabel = (label: string, hint: string): ReactNode => (
  <Tooltip title={hint}>
    <span>
      {label} <InfoCircleOutlined />
    </span>
  </Tooltip>
);

/** Nội dung lịch KPI: một dải tổng hợp chỉ tiêu và BTP khả dụng, lịch tháng để đặt KPI từng ngày ở dưới. */
export function KpiCalendarBody({ data, editable }: KpiCalendarBodyProps) {
  const plan = useKpiDailyPlan(data);
  const save = useSaveKpiDailyPlan(data.kpiTargetId);

  const renderDay = (date: string) => {
    const day = plan.byDate.get(date);
    if (!day) return null;
    const qty = plan.qtyOf(day);
    const behind = date < data.today && qty > 0 && (day.actualQty ?? 0) < qty;
    return (
      <>
        {editable && !plan.isLocked(day) ? (
          <InputNumber
            size="small"
            min={0}
            precision={0}
            controls={false}
            aria-label={fill(text.dayInput, date)}
            placeholder={text.kpi}
            value={qty > 0 ? qty : null}
            status={plan.isChanged(day) ? 'warning' : undefined}
            onChange={(value) => plan.setQty(day, value)}
            style={{ width: '100%' }}
          />
        ) : (
          <Typography.Text style={SMALL}>
            {text.kpi}: <b>{qty > 0 ? formatQty(qty) : '—'}</b>
          </Typography.Text>
        )}
        {day.actualQty !== null && (
          <Typography.Text type={behind ? 'danger' : 'secondary'} style={SMALL}>
            {text.actual}: {formatQty(day.actualQty)}
          </Typography.Text>
        )}
      </>
    );
  };

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Descriptions
        size="small"
        bordered
        layout="vertical"
        column={{ xs: 2, sm: 2, md: 3, lg: 6, xl: 6, xxl: 6 }}
        items={[
          { key: 'd', label: text.dueDate, children: <b>{formatDate(data.dueDate)}</b> },
          { key: 't', label: text.total, children: formatQty(data.targetFinishedQty) },
          { key: 'f', label: text.finished, children: formatQty(data.finishedQty) },
          {
            key: 'a',
            label: text.allocated,
            children: (
              <Typography.Text type={plan.overTotal ? 'danger' : undefined}>
                {fill(text.allocatedValue, formatQty(plan.allocated), formatQty(plan.unallocated))}
              </Typography.Text>
            ),
          },
          {
            key: 'b',
            label: hintLabel(
              text.btpAvailable,
              fill(
                text.btpDetail,
                formatQty(data.btp.warehouseBtp),
                formatQty(data.btp.teamHeld),
                formatQty(data.btp.neededByOthers),
              ),
            ),
            children: formatQty(data.btp.available),
          },
          {
            key: 'l',
            label: hintLabel(text.btpLeft, text.btpLeftHint),
            children: (
              <Typography.Text type={plan.btpLeft < 0 ? 'danger' : undefined}>
                {formatQty(plan.btpLeft)}
              </Typography.Text>
            ),
          },
        ]}
      />

      {plan.overTotal && <Alert type="error" showIcon message={text.overTotal} />}
      {plan.overBtp && <Alert type="error" showIcon message={text.overBtp} />}
      {plan.afterDueDays > 0 && (
        <Alert
          type="warning"
          showIcon
          message={fill(text.afterDue, plan.afterDueDays, formatDate(data.dueDate))}
        />
      )}
      {!editable && <Alert type="info" showIcon message={text.readOnly} />}

      {editable && (
        <Flex justify="space-between" align="center" wrap gap={8}>
          <Typography.Text type="secondary" style={SMALL}>
            {fill(text.pastLocked, data.editCutoff.slice(0, TIME_LENGTH))}
          </Typography.Text>
          <Space>
            <Button size="small" onClick={plan.reset} disabled={!plan.dirty}>
              {text.reset}
            </Button>
            <Button
              size="small"
              type="primary"
              loading={save.isPending}
              disabled={!plan.dirty || plan.overTotal || plan.overBtp}
              onClick={() => save.mutate(plan.payload, { onSuccess: plan.reset })}
            >
              {text.save}
            </Button>
          </Space>
        </Flex>
      )}

      <KpiMonthCalendar
        startDate={data.startDate}
        endDate={data.endDate}
        today={data.today}
        dueDate={data.dueDate}
        renderDay={renderDay}
      />
    </Space>
  );
}
