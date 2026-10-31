import { Alert, Button, Modal, Typography, message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { TempPassword } from '../types';

type TempPasswordModalProps = { value: TempPassword | null; onClose: () => void };

/** Mật khẩu tạm chỉ hiện một lần (D29): không lưu store / localStorage, không log; đóng modal là mất. */
export function TempPasswordModal({ value, onClose }: TempPasswordModalProps) {
  const copy = async () => {
    if (!value) return;
    await navigator.clipboard.writeText(value.temporaryPassword);
    void message.success(MESSAGES.hr.copied);
  };

  return (
    <Modal
      open={value !== null}
      title={MESSAGES.hr.tempPasswordTitle}
      onCancel={onClose}
      destroyOnHidden
      footer={[
        <Button key="copy" onClick={() => void copy()}>
          {MESSAGES.hr.copy}
        </Button>,
        <Button key="close" type="primary" onClick={onClose}>
          {MESSAGES.common.close}
        </Button>,
      ]}
    >
      {value && (
        <>
          <Alert
            type="warning"
            showIcon
            message={MESSAGES.hr.tempPasswordWarning}
            style={{ marginBottom: 16 }}
          />
          <div>
            {`${MESSAGES.hr.username}: `}
            <Typography.Text strong>{value.username}</Typography.Text>
          </div>
          <Typography.Title level={2} copyable={false} style={{ letterSpacing: 6, marginTop: 8 }}>
            {value.temporaryPassword}
          </Typography.Title>
        </>
      )}
    </Modal>
  );
}
