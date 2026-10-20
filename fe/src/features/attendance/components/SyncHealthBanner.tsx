import { Alert, Space, Tag, Typography } from 'antd';
import { CheckCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { ATTENDANCE_TEXT } from '../constants';
import type { SyncHealth } from '../types';
import { formatDateTime } from '../utils';

/**
 * MSG23: quá ngưỡng ATTENDANCE_ALERT_MINUTES mà chưa đồng bộ được thì cảnh báo,
 * kèm gợi ý nhập file dự phòng. Bình thường vẫn hiện một dòng xanh để Kế toán yên tâm.
 */
export function SyncHealthBanner({ health }: { health: SyncHealth | undefined }) {
  if (!health) return null;

  const lastText =
    health.lastSucceededAt === null
      ? ATTENDANCE_TEXT.sync.never
      : formatDateTime(health.lastSucceededAt);

  if (!health.isOverdue) {
    return (
      <Alert
        type="success"
        showIcon
        icon={<CheckCircleOutlined />}
        message={
          <Space size={8} wrap>
            <span>{ATTENDANCE_TEXT.sync.healthyTitle}</span>
            <Typography.Text type="secondary">
              {ATTENDANCE_TEXT.stats.lastSync}: {lastText}
            </Typography.Text>
          </Space>
        }
      />
    );
  }

  return (
    <Alert
      type="warning"
      showIcon
      icon={<WarningOutlined />}
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
