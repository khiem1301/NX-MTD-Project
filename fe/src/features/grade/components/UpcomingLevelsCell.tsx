import { Popover, Space, Tag } from 'antd';
import { formatCurrency, formatDate } from '@/utils/format';
import { GR_LABELS } from '../constants';
import type { GradeLevel } from '../types';

/** Mức sắp áp dụng gần nhất trên một dòng; còn nhiều mức thì thẻ +N mở danh sách đầy đủ. */
export function UpcomingLevelsCell({ levels }: { levels: GradeLevel[] }) {
  const first = levels[0];
  if (!first) return null;
  return (
    <Space size={4} wrap={false}>
      <Tag color="blue" style={{ marginInlineEnd: 0 }}>
        {GR_LABELS.upcoming}
      </Tag>
      <span style={{ whiteSpace: 'nowrap' }}>
        {`${formatCurrency(first.monthlyAmount)} · ${formatDate(first.effectiveDate)}`}
      </span>
      {levels.length > 1 && (
        <Popover
          title={GR_LABELS.upcoming}
          content={
            <div style={{ maxHeight: 200, overflowY: 'auto' }}>
              {levels.map((n) => (
                <div key={n.gradeLevelId} style={{ whiteSpace: 'nowrap' }}>
                  {`${formatDate(n.effectiveDate)} · ${formatCurrency(n.monthlyAmount)}`}
                </div>
              ))}
            </div>
          }
        >
          <Tag style={{ cursor: 'pointer', marginInlineEnd: 0 }}>{`+${levels.length - 1}`}</Tag>
        </Popover>
      )}
    </Space>
  );
}
