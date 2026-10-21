import { Alert, Avatar, Card, Descriptions, Space, Spin } from 'antd';
import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import { ChangePasswordForm } from '../components/ChangePasswordForm';
import { useProfile } from '../hooks/useProfile';

/** UC04: hồ sơ chỉ đọc (không có kiểu lương, mã chấm công) + UC03 đổi mật khẩu. */
export function ProfilePage() {
  const profile = useProfile();
  const [params] = useSearchParams();
  const formRef = useRef<HTMLDivElement>(null);
  const mustChange = params.get('changePassword') === '1';

  useEffect(() => {
    if (mustChange) formRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [mustChange]);

  if (profile.isLoading) return <Spin />;
  const p = profile.data;

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      {mustChange && <Alert type="warning" showIcon message={MESSAGES.hr.tempPasswordNotice} />}
      <Card title={MESSAGES.hr.profileTitle}>
        <QueryError error={profile.error} onRetry={() => void profile.refetch()} />
        {p && (
          <Space align="start" size="large" wrap>
            <Avatar size={72} src={p.avatarUrl ?? undefined}>{p.fullName.slice(0, 1)}</Avatar>
            <Descriptions column={2} size="small" bordered>
              <Descriptions.Item label={MESSAGES.hr.employeeCode}>{p.employeeCode}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.fullName}>{p.fullName}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.role}>{p.roleName}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.team}>{p.teamName ?? '—'}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.startDate}>{formatDate(p.startDate)}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.dateOfBirth}>{formatDate(p.dateOfBirth)}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.phone}>{p.phone ?? '—'}</Descriptions.Item>
              {p.currentGradeCode && <Descriptions.Item label={MESSAGES.hr.grade}>{p.currentGradeCode}</Descriptions.Item>}
            </Descriptions>
          </Space>
        )}
      </Card>
      <div ref={formRef}>
        <Card title={MESSAGES.hr.changePasswordTitle}>
          <ChangePasswordForm />
        </Card>
      </div>
    </Space>
  );
}
