import type { TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { AuditEntry } from '@/types/attachment';
import { describeChanges } from '@/utils/auditDiff';
import { auditActionText } from '@/utils/auditLabels';
import { formatDateTime } from '@/utils/format';
import { DataTable } from './DataTable';

/** Nội dung một dòng: các trường đổi giá trị, không có thì dùng câu mô tả BE ghi sẵn. */
const contentOf = (h: AuditEntry) => {
  const changes = describeChanges(h.oldValues, h.newValues);
  return changes.length > 0 ? changes.map((c) => <div key={c}>{c}</div>) : (h.summary ?? '—');
};

const columns: TableColumnsType<AuditEntry> = [
  {
    title: MESSAGES.common.historyTime,
    dataIndex: 'changedAt',
    width: 150,
    render: (v: string) => formatDateTime(v),
  },
  {
    title: MESSAGES.common.historyBy,
    dataIndex: 'changedByName',
    width: 170,
    render: (v: string | null) => v ?? '—',
  },
  {
    title: MESSAGES.common.historyAction,
    dataIndex: 'action',
    width: 120,
    render: (v: AuditEntry['action']) => auditActionText(v),
  },
  {
    title: MESSAGES.common.historyContent,
    key: 'content',
    render: (_: unknown, h) => contentOf(h),
  },
  { title: MESSAGES.common.reason, dataIndex: 'reason', render: (v: string | null) => v ?? '—' },
];

/** Lịch sử thay đổi (AuditLog) của đơn hàng, mã hàng, đơn mua, nhân viên, tổ: bảng có phân trang, mới nhất ở trên. Lịch sử đơn hàng chứa đơn giá nên chỉ Admin xem (D26). */
export function AuditHistoryTable({ history }: { history: AuditEntry[] }) {
  const rows = [...history].reverse();
  return <DataTable<AuditEntry> rowKey="auditLogId" columns={columns} dataSource={rows} />;
}
