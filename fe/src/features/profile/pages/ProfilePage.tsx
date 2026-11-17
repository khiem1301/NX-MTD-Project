import { Avatar, Button, Card, Descriptions, Space, Spin } from 'antd';
import { useState } from 'react';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import { ProfileEditModal } from '../components/ProfileEditModal';
import { useProfile } from '../hooks/useProfile';

/**
 * UC04: hồ sơ (không có kiểu lương, mã chấm công); nút Sửa mở hộp thoại sửa ngày sinh / số điện thoại và UC03 đổi mật khẩu.
 * Mật khẩu tạm được xử lý riêng ở ForceChangePasswordPage.
 */
export function ProfilePage() {
  const profile = useProfile();
  const [editing, setEditing] = useState(false);

  if (profile.isLoading) return <Spin />;
  const p = profile.data;

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card
        title={MESSAGES.hr.profileTitle}
        extra={p && <Button onClick={() => setEditing(true)}>{MESSAGES.common.edit}</Button>}
      >
        <QueryError error={profile.error} onRetry={() => void profile.refetch()} />
        {p && (
          <Space align="start" size="large" wrap>
            <Avatar size={72} src={p.avatarUrl ?? undefined}>
              {p.fullName.slice(0, 1)}
            </Avatar>
            <Descriptions column={2} size="small" bordered>
              <Descriptions.Item label={MESSAGES.hr.employeeCode}>
                {p.employeeCode}
              </Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.username}>{p.username}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.fullName}>{p.fullName}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.role}>{p.roleName}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.team}>{p.teamName ?? '—'}</Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.startDate}>
                {formatDate(p.startDate)}
              </Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.dateOfBirth}>
                {formatDate(p.dateOfBirth)}
              </Descriptions.Item>
              <Descriptions.Item label={MESSAGES.hr.phone}>{p.phone ?? '—'}</Descriptions.Item>
              {p.currentGradeCode && (
                <Descriptions.Item label={MESSAGES.hr.grade}>
                  {p.currentGradeCode}
                </Descriptions.Item>
              )}
            </Descriptions>
          </Space>
        )}
      </Card>
      {p && <ProfileEditModal open={editing} profile={p} onClose={() => setEditing(false)} />}
    </Space>
  );
}
