import { Descriptions, Empty, Tag, Typography, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DetailDescriptions, DetailModal } from '@/components/common/DetailModal';
import { buildChangeRows, type ChangeRow } from '@/utils/auditDiff';
import { AUDIT_ACTION, auditSummary, auditTableLabel } from '@/utils/auditLabels';
import { formatDateTime } from '@/utils/format';
import type { AuditRow } from '../types';

const diffColumns: TableColumnsType<ChangeRow> = [
  { title: 'Trường', dataIndex: 'field', width: 220 },
  { title: 'Trước', dataIndex: 'before' },
  { title: 'Sau', dataIndex: 'after' },
];

const valueColumns: TableColumnsType<ChangeRow> = [
  { title: 'Trường', dataIndex: 'field', width: 220 },
  { title: 'Giá trị', dataIndex: 'after' },
];

/** Chi tiết một bản ghi nhật ký: ai, lúc nào, làm gì và bảng "trường – trước – sau" thay cho dữ liệu JSON thô. */
export function AuditLogDetailModal({
  row,
  onClose,
}: {
  row: AuditRow | null;
  onClose: () => void;
}) {
  const rows = row ? buildChangeRows(row.oldValues, row.newValues) : [];
  const hasBefore = rows.some((r) => r.before !== null);
  const action = row ? AUDIT_ACTION[row.action] : undefined;

  return (
    <DetailModal open={row !== null} onClose={onClose} title="Chi tiết thay đổi" size="md">
      {row && (
        <>
          <DetailDescriptions style={{ marginBottom: 16 }}>
            <Descriptions.Item label="Thời gian">{formatDateTime(row.changedAt)}</Descriptions.Item>
            <Descriptions.Item label="Người thực hiện">
              {row.changedByName ?? 'Hệ thống'}
            </Descriptions.Item>
            <Descriptions.Item label="Nội dung" span={2}>
              {auditSummary(row)}
            </Descriptions.Item>
            <Descriptions.Item label="Đối tượng">
              {`${auditTableLabel(row.tableName)} #${row.recordId}`}
            </Descriptions.Item>
            <Descriptions.Item label="Hành động">
              <Tag color={action?.color ?? 'default'}>{action?.text ?? row.action}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Lý do" span={2}>
              {row.reason ?? '—'}
            </Descriptions.Item>
          </DetailDescriptions>
          <Typography.Title level={5}>Nội dung thay đổi</Typography.Title>
          {rows.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có chi tiết giá trị" />
          ) : (
            <DataTable<ChangeRow>
              rowKey={(r) => `${r.field}|${r.after}`}
              size="small"
              pagination={false}
              columns={hasBefore ? diffColumns : valueColumns}
              dataSource={rows}
            />
          )}
        </>
      )}
    </DetailModal>
  );
}
