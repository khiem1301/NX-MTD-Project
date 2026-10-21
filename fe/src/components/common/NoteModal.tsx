import { Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';

type NoteModalProps = {
  open: boolean;
  title: string;
  label: string;
  required: boolean;
  confirmText?: string;
  danger?: boolean;
  loading?: boolean;
  onCancel: () => void;
  onSubmit: (note: string | null) => void;
};

type NoteForm = { note?: string };

const NOTE_MAX_LENGTH = 1000;

/** Hộp nhập lý do / ghi chú; bắt buộc hoặc không (hủy, dừng, khóa, kết quả xử lý...). Bắt buộc nhập thì dùng được ReasonModal. */
export function NoteModal({ open, title, label, required, confirmText, danger, loading, onCancel, onSubmit }: NoteModalProps) {
  const [form] = Form.useForm<NoteForm>();

  const handleFinish = (values: NoteForm) => {
    const note = values.note?.trim() ?? '';
    onSubmit(note === '' ? null : note);
  };

  return (
    <Modal
      open={open}
      title={title}
      okText={confirmText ?? MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ danger }}
      confirmLoading={loading}
      destroyOnHidden
      onCancel={onCancel}
      onOk={() => form.submit()}
    >
      <Form form={form} layout="vertical" preserve={false} onFinish={handleFinish}>
        <Form.Item
          name="note"
          label={label}
          rules={required ? [{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }] : []}
        >
          <Input.TextArea rows={3} maxLength={NOTE_MAX_LENGTH} showCount autoFocus />
        </Form.Item>
      </Form>
    </Modal>
  );
}
