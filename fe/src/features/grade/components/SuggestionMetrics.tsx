import { Space, Tooltip, Typography } from 'antd';
import { fill, formatNumber, formatQty } from '@/utils/format';
import { AI_LABELS } from '../constants';
import type { GradeWorkerMetrics } from '../types';

const SMALL = { fontSize: 12 } as const;

/** Số liệu chính của kỳ xét bậc; rê chuột để xem thêm giờ làm, thâm niên, độ khó công đoạn. */
export function SuggestionMetrics({ metrics: m }: { metrics: GradeWorkerMetrics }) {
  const defectRate =
    m.defectRatePercent === null
      ? ''
      : fill(AI_LABELS.defectRate, formatNumber(m.defectRatePercent));
  const lines = [
    fill(AI_LABELS.workDays, formatNumber(m.workdayTotal)),
    fill(AI_LABELS.output, formatQty(m.outputQty), m.operationCount),
    m.efficiencyPercent === null
      ? null
      : fill(AI_LABELS.efficiency, formatNumber(m.efficiencyPercent)),
    fill(AI_LABELS.defects, m.reworkQty, m.scrapQty) + defectRate,
  ].filter((l): l is string => l !== null);
  const details = [
    fill(AI_LABELS.hours, formatNumber(m.workedHours), formatNumber(m.overtimeHours)),
    fill(AI_LABELS.tenure, m.tenureMonths),
    m.avgSkillLevel === null ? null : fill(AI_LABELS.skill, formatNumber(m.avgSkillLevel)),
  ].filter((l): l is string => l !== null);
  return (
    <Tooltip
      title={
        <Space direction="vertical" size={0}>
          {details.map((d) => (
            <span key={d}>{d}</span>
          ))}
        </Space>
      }
    >
      <Space direction="vertical" size={0}>
        {lines.map((l) => (
          <Typography.Text key={l} style={SMALL}>
            {l}
          </Typography.Text>
        ))}
      </Space>
    </Tooltip>
  );
}
