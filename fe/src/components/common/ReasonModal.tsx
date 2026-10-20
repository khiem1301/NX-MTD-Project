import { Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';

type ReasonModalProps = {
  open: boolean;
  title: string;
  loading?: boolean;
  onSubmit: (reason: string) => void;
  onCancel: () => void;
};

const REASON_MAX_LENGTH = 500;

/** Hộp nhập lý do bắt buộc cho thao tác từ chối / trả lại. */
export function ReasonModal({ open, title, loading, onSubmit, onCancel }: ReasonModalProps) {
  const [form] = Form.useForm<{ reason: string }>();

  return (
    <Modal
      open={open}
      title={title}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={loading}
      destroyOnHidden
      onCancel={onCancel}
      onOk={() => form.submit()}
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) => onSubmit(values.reason.trim())}
      >
        <Form.Item
          name="reason"
          label={MESSAGES.common.reason}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}
        >
          <Input.TextArea rows={3} maxLength={REASON_MAX_LENGTH} showCount autoFocus />
        </Form.Item>
      </Form>
    </Modal>
  );
}
