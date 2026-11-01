import { Alert, Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { DEFECT_TEXT_MAX, PRODUCTION_TEXT } from '../constants';
import type { Defect } from '../types';

const text = PRODUCTION_TEXT.defects;

type ConfirmDefectModalProps = {
  defect: Defect | null;
  loading: boolean;
  onCancel: () => void;
  onSubmit: (note: string | undefined) => void;
};

/**
 * Quản đốc / Admin chấp nhận phiếu sau khi công nhân phản đối: phiếu được giữ nguyên. Hiện nội dung phản đối để đọc trước;
 * ghi chú không bắt buộc (ghi vào nhật ký). Không chấp nhận thì dùng nút hủy phiếu.
 */
export function ConfirmDefectModal({
  defect,
  loading,
  onCancel,
  onSubmit,
}: ConfirmDefectModalProps) {
  const [form] = Form.useForm<{ note?: string }>();

  return (
    <Modal
      open={defect !== null}
      title={text.confirmTitle}
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={loading}
      destroyOnHidden
      onCancel={onCancel}
      onOk={() => form.submit()}
    >
      {defect && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={`${text.confirmWorker} ${defect.responsibleWorkerName}`}
          description={defect.workerObjection}
        />
      )}
      <p>{text.confirmHint}</p>
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        disabled={loading}
        onFinish={({ note }) => onSubmit(note?.trim() || undefined)}
      >
        <Form.Item name="note" label={text.confirmNote}>
          <Input.TextArea rows={3} maxLength={DEFECT_TEXT_MAX} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
