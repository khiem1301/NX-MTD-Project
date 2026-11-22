import { useEffect, useState } from 'react';
import { Alert, Space, Tag } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { formatDateTime } from '@/utils/format';
import { ATTENDANCE_TEXT } from '../constants';
import type { SyncHealth } from '../types';

const AUTO_HIDE_MS = 20_000;

/**
 * MSG23: hiện khi quá hạn đồng bộ, tự ẩn sau 20 giây (hoặc bấm đóng).
 * Vào lại trang / health đổi trạng thái overdue thì hiện lại.
 */
export function SyncHealthBanner({ health }: { health: SyncHealth | undefined }) {
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  const [hidden, setHidden] = useState(false);

  const overdueKey =
    health?.isOverdue === true
      ? `${health.lastSucceededAt ?? 'never'}|${health.alertThresholdMinutes}`
      : null;

  useEffect(() => {
    if (overdueKey === null) {
      setHidden(false);
      return;
    }
    setHidden(false);
    const timer = window.setTimeout(() => setHidden(true), AUTO_HIDE_MS);
    return () => window.clearTimeout(timer);
  }, [overdueKey]);

  if (!health?.isOverdue || hidden) return null;

  const lastText =
    health.lastSucceededAt === null
      ? ATTENDANCE_TEXT.sync.never
      : formatDateTime(health.lastSucceededAt);

  return (
    <Alert
      type="warning"
      showIcon
      closable
      onClose={() => setHidden(true)}
      icon={<WarningOutlined />}
      style={{ marginBottom: 16 }}
      message={ATTENDANCE_TEXT.sync.overdueTitle.replace(
        '{0}',
        String(health.alertThresholdMinutes),
      )}
      description={
        <Space direction="vertical" size={4}>
          <span>{ATTENDANCE_TEXT.sync.overdueHint}</span>
          <span>
            {isAdmin ? ATTENDANCE_TEXT.sync.overdueHintAdmin : ATTENDANCE_TEXT.sync.overdueHintOther}
          </span>
          <Tag color="orange">
            {ATTENDANCE_TEXT.stats.lastSync}: {lastText}
          </Tag>
        </Space>
      }
    />
  );
}
