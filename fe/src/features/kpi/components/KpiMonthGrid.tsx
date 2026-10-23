import type { ReactNode } from 'react';
import { theme } from 'antd';
import dayjs from 'dayjs';
import { CAL_TEXT } from '../constants';
import { buildMonthGrid } from '../utils/dailyPlan';

type Props = {
  /** Ngày bất kỳ trong tháng cần hiện (YYYY-MM-DD). */
  month: string;
  /** Nội dung một ô ngày; trả null = ngày ngoài kỳ KPI (ô mờ, không có nội dung). */
  renderDay: (date: string) => ReactNode | null;
};

const TITLE_GREEN = '#006c2e';
const HEAD_GREEN = '#1b7f2c';
const SAT_GREEN = '#389e0d';
const SUN_ORANGE = '#d46b08';

/** Lịch tháng kiểu tờ lịch: tiêu đề "THÁNG x NĂM y", cột Thứ hai → Chủ nhật, Thứ bảy xanh, Chủ nhật cam. */
export function KpiMonthGrid({ month, renderDay }: Props) {
  const { token } = theme.useToken();
  const weeks = buildMonthGrid(month);
  const m = dayjs(month);
  const dayColor = (col: number): string | undefined => (col === 5 ? SAT_GREEN : col === 6 ? SUN_ORANGE : undefined);

  return (
    <div style={{ border: `1px solid ${HEAD_GREEN}`, borderRadius: 8, overflow: 'hidden' }}>
      <div style={{ background: TITLE_GREEN, color: '#fff', textAlign: 'center', fontWeight: 700, padding: '10px 0', letterSpacing: 1 }}>
        {CAL_TEXT.monthTitle(m.month() + 1, m.year())}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', background: HEAD_GREEN }}>
        {CAL_TEXT.weekdays.map((name) => (
          <div key={name} style={{ color: '#fff', textAlign: 'center', fontWeight: 600, padding: '6px 0', fontSize: 13 }}>
            {name.toUpperCase()}
          </div>
        ))}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: 1, background: token.colorBorderSecondary }}>
        {weeks.flatMap((week, wi) =>
          week.map((date, col) => {
            const key = `${wi}-${col}`;
            if (date === null) return <div key={key} style={{ background: '#5cb85c', opacity: 0.55, minHeight: 112 }} />;
            const content = renderDay(date);
            const number = (
              <div style={{ fontWeight: 700, fontSize: 16, color: content === null ? token.colorTextQuaternary : dayColor(col) }}>
                {dayjs(date).date()}
              </div>
            );
            return (
              <div
                key={key}
                style={{
                  background: content === null ? token.colorFillQuaternary : token.colorBgContainer,
                  minHeight: 112,
                  padding: '6px 8px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                }}
              >
                {number}
                {content}
              </div>
            );
          }),
        )}
      </div>
    </div>
  );
}
