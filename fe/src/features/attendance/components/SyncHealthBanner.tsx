import { Alert, Space, Tag } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import { ATTENDANCE_TEXT } from '../constants';
import type { SyncHealth } from '../types';
import { formatDateTime } from '@/utils/format';

/**
 * MSG23: chỉ hiện khi quá hạn đồng bộ. Trạng thái bình thường không gắn banner cố định
 * (thông báo tạm khi bấm Đồng bộ từ RJ).
 */
export function SyncHealthBanner({ health }: { health: SyncHealth | undefined }) {
  if (!health?.isOverdue) return null;

  const lastText =
    health.lastSucceededAt === null
      ? ATTENDANCE_TEXT.sync.never
      : formatDateTime(health.lastSucceededAt);

  return (
    <Alert
      type="warning"
      showIcon
      icon={<WarningOutlined />}
      style={{ marginBottom: 16 }}
      message={ATTENDANCE_TEXT.sync.overdueTitle.replace(
        '{0}',
        String(health.alertThresholdMinutes),
      )}
      description={
        <Space size={8} wrap>
          <span>{ATTENDANCE_TEXT.sync.overdueHint}</span>
          <Tag color="orange">
            {ATTENDANCE_TEXT.stats.lastSync}: {lastText}
          </Tag>
        </Space>
      }
    />
  );
}
