import { Button, Divider, Form, Input, message, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DetailModal } from '@/components/common/DetailModal';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { toApiDateOrNull } from '@/utils/format';
import { normalizePhone, phoneRule } from '@/utils/phone';
import type { ChangePasswordPayload, Profile } from '../api/profileApi';
import { useChangePassword, useUpdateProfile } from '../hooks/useProfile';
import { PasswordFields } from './PasswordFields';
import { showPasswordError } from './passwordErrors';

type FormValues = Partial<ChangePasswordPayload> & {
  dateOfBirth?: Dayjs;
  phone?: string;
};

type Props = {
  open: boolean;
  profile: Profile;
  onClose: () => void;
};

const PASSWORD_FIELDS = ['currentPassword', 'newPassword', 'confirmPassword'] as const;

/**
 * UC04 + UC03: một hộp thoại cho cả ngày sinh / số điện thoại và đổi mật khẩu. Để trống ba ô mật khẩu là không đổi.
 * Đổi mật khẩu trước: sai mật khẩu hiện tại (lỗi hay gặp nhất) thì chưa lưu gì cả.
 */
export function ProfileEditModal({ open, profile, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const update = useUpdateProfile();
  const changePassword = useChangePassword();
  const saving = update.isPending || changePassword.isPending;

  const showFieldErrors = (error: ApiError) =>
    form.setFields(
      Object.entries(error.errors ?? {}).map(([name, errors]) => ({
        name: name as keyof FormValues,
        errors,
      })),
    );

  const handleFinish = async (v: FormValues) => {
    if (PASSWORD_FIELDS.some((name) => v[name])) {
      try {
        await changePassword.mutateAsync({
          currentPassword: v.currentPassword,
          newPassword: v.newPassword ?? '',
          confirmPassword: v.confirmPassword ?? '',
        });
      } catch (error) {
        if (!showPasswordError(form, error as ApiError)) void message.error((error as ApiError).message);
        return;
      }
      void message.success(MESSAGES.hr.passwordChanged);
      form.resetFields([...PASSWORD_FIELDS]);
    }

    const dateOfBirth = toApiDateOrNull(v.dateOfBirth);
    const phone = v.phone || null;
    if (dateOfBirth === profile.dateOfBirth && phone === profile.phone) {
      onClose();
      return;
    }
    update.mutate({ dateOfBirth, phone }, { onSuccess: onClose, onError: showFieldErrors });
  };

  return (
    <DetailModal
      open={open}
      title={MESSAGES.hr.editProfileTitle}
      onClose={onClose}
      size="sm"
      extra={
        <Space>
          <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
          <Button type="primary" loading={saving} onClick={() => form.submit()}>
            {MESSAGES.common.save}
          </Button>
        </Space>
      }
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        initialValues={{
          dateOfBirth: profile.dateOfBirth ? dayjs(profile.dateOfBirth) : undefined,
          phone: profile.phone ?? undefined,
        }}
        onFinish={handleFinish}
        disabled={saving}
      >
        <Form.Item name="dateOfBirth" label={MESSAGES.hr.dateOfBirth}>
          <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item
          name="phone"
          label={MESSAGES.hr.phone}
          normalize={normalizePhone}
          rules={[phoneRule]}
        >
          <Input maxLength={12} style={{ width: 160 }} inputMode="tel" />
        </Form.Item>
        <Divider orientation="left">{MESSAGES.hr.changePasswordTitle}</Divider>
        <Typography.Paragraph type="secondary">{MESSAGES.hr.passwordOptionalHint}</Typography.Paragraph>
        <PasswordFields optional />
      </Form>
    </DetailModal>
  );
}
