import { Alert, Button, Card, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useLogout } from '@/features/auth';
import { ChangePasswordForm } from '../components/ChangePasswordForm';

/**
 * D29: màn toàn trang như màn đăng nhập, hiện khi tài khoản đang dùng mật khẩu tạm (tạo mới / đặt lại).
 * Đổi xong, token mới hết cờ mật khẩu tạm nên ProtectedRoute tự cho vào hệ thống.
 */
export function ForceChangePasswordPage() {
  const logout = useLogout();

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 16 }}>
      <Card style={{ width: 420, maxWidth: '100%' }}>
        <Typography.Title level={3} style={{ marginBottom: 16 }}>
          {MESSAGES.hr.changePasswordTitle}
        </Typography.Title>
        <Alert
          type="warning"
          showIcon
          message={MESSAGES.hr.tempPasswordNotice}
          style={{ marginBottom: 16 }}
        />
        <ChangePasswordForm requireCurrent={false} />
        <Button type="link" onClick={() => void logout()} style={{ paddingLeft: 0 }}>
          Đăng xuất
        </Button>
      </Card>
    </div>
  );
}
