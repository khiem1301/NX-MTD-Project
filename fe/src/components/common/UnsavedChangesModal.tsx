import { Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';

type UnsavedChangesModalProps = {
  open: boolean;
  /** Rời trang, bỏ các thay đổi chưa lưu. */
  onLeave: () => void;
  /** Ở lại trang để lưu tiếp (nút "Ở lại", nút đóng hoặc phím Esc). */
  onStay: () => void;
};

/** Hộp hỏi khi rời trang còn thay đổi chưa lưu, thay cho `window.confirm` của trình duyệt. */
export function UnsavedChangesModal({ open, onLeave, onStay }: UnsavedChangesModalProps) {
  const text = MESSAGES.common.unsaved;
  return (
    <Modal
      open={open}
      centered
      width={420}
      title={text.title}
      okText={text.leave}
      cancelText={text.stay}
      okButtonProps={{ danger: true }}
      maskClosable={false}
      onOk={onLeave}
      onCancel={onStay}
    >
      {text.body}
    </Modal>
  );
}
