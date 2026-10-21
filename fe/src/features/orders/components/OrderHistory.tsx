import { Timeline, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { AuditEntry } from '@/types/attachment';
import { formatDateTime } from '@/utils/format';
import { describeChanges } from '../utils/auditDiff';

const ACTION_LABEL: Partial<Record<AuditEntry['action'], string>> = { INSERT: 'Tạo', UPDATE: 'Cập nhật' };

/** Lịch sử đơn hàng / mã hàng. Đơn hàng chứa đơn giá nên chỉ Admin xem (D26). */
export function OrderHistory({ history }: { history: AuditEntry[] }) {
  return (
    <Timeline
      items={history.map((h) => ({
        key: h.auditLogId,
        children: (
          <>
            <Typography.Text strong>{ACTION_LABEL[h.action] ?? h.action}</Typography.Text>
            <div>{`${h.changedByName ?? '—'} · ${formatDateTime(h.changedAt)}`}</div>
            {describeChanges(h.oldValues, h.newValues).map((c) => (
              <div key={c}>{c}</div>
            ))}
            {h.reason && <div>{`${MESSAGES.common.reason}: ${h.reason}`}</div>}
          </>
        ),
      }))}
    />
  );
}
