import { Timeline, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { AuditEntry } from '@/types/attachment';
import { formatDateTime } from '@/utils/format';
import { AUDIT_ACTION_LABEL } from '../constants';

type RequestHistoryProps = {
  createdByName: string | null;
  createdAt: string;
  history: AuditEntry[];
};

/** Nhãn "Hủy đơn" khi bản ghi UPDATE đổi Status sang CANCELLED; JSON hỏng thì dùng nhãn mặc định. */
const entryLabel = (entry: AuditEntry): string => {
  if (entry.action === 'UPDATE' && entry.newValues) {
    try {
      const parsed = JSON.parse(entry.newValues) as { Status?: string };
      if (parsed.Status === 'CANCELLED') return MESSAGES.requests.historyCancelled;
    } catch {
      /* newValues không phải JSON: bỏ qua */
    }
  }
  return AUDIT_ACTION_LABEL[entry.action] ?? entry.action;
};

/** D18: dòng "Tạo đơn" lấy từ chính đơn, không có bản ghi AuditLog INSERT. */
export function RequestHistory({ createdByName, createdAt, history }: RequestHistoryProps) {
  const items = [
    {
      key: 'created',
      children: (
        <>
          <Typography.Text strong>{MESSAGES.requests.historyCreated}</Typography.Text>
          <div>{`${createdByName ?? '—'} · ${formatDateTime(createdAt)}`}</div>
        </>
      ),
    },
    ...history.map((h) => ({
      key: String(h.auditLogId),
      children: (
        <>
          <Typography.Text strong>{entryLabel(h)}</Typography.Text>
          <div>{`${h.changedByName ?? '—'} · ${formatDateTime(h.changedAt)}`}</div>
          {h.reason && <div>{`${MESSAGES.common.reason}: ${h.reason}`}</div>}
        </>
      ),
    })),
  ];
  return <Timeline items={items} />;
}
