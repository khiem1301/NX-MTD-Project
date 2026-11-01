import { EyeOutlined } from '@ant-design/icons';
import { Tag, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { AUDIT_ACTION, auditSummary } from '@/utils/auditLabels';
import { formatDateTime } from '@/utils/format';
import type { AuditRow } from '../types';

/** Cột bảng nhật ký: thời gian, nội dung (câu mô tả đối tượng và thay đổi), người thực hiện, hành động, lý do; nút Xem chi tiết ở cột thao tác. */
export const auditLogColumns = (onView: (row: AuditRow) => void): TableColumnsType<AuditRow> => [
  { title: 'Thời gian', dataIndex: 'changedAt', width: 160, render: formatDateTime },
  {
    title: 'Nội dung',
    key: 'summary',
    render: (_, r) => auditSummary(r),
  },
  {
    title: 'Người thực hiện',
    dataIndex: 'changedByName',
    render: (v: string | null) => v ?? 'Hệ thống',
  },
  {
    title: 'Hành động',
    dataIndex: 'action',
    render: (v: string) => (
      <Tag color={AUDIT_ACTION[v]?.color ?? 'default'}>{AUDIT_ACTION[v]?.text ?? v}</Tag>
    ),
  },
  { title: 'Lý do', dataIndex: 'reason', render: (v: string | null) => v ?? '—' },
  {
    title: MESSAGES.common.actions,
    key: 'actions',
    render: (_, r) => (
      <IconAction title="Xem chi tiết" icon={<EyeOutlined />} onClick={() => onView(r)} />
    ),
  },
];
