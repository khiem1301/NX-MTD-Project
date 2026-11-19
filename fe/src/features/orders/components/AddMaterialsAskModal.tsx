import { Button, Modal, Space, theme } from 'antd';
import { ExclamationCircleFilled } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';

const text = MESSAGES.orders.excelReview;
const ASK_WIDTH = 480;

type AddMaterialsAskModalProps = {
  open: boolean;
  codes: string[];
  onAdd: () => void;
  onSkip: () => void;
  /** Nút X / Esc: đóng hộp, quay lại popup kiểm tra, chưa chọn gì. */
  onClose: () => void;
};

/**
 * DEV-ALL-123: hỏi admin có thêm các NVL chưa có vào danh mục khi lưu không. Hộp riêng (không dùng Modal.confirm, vì nút X của
 * Modal.confirm chạy như "Không thêm").
 */
export function AddMaterialsAskModal({
  open,
  codes,
  onAdd,
  onSkip,
  onClose,
}: AddMaterialsAskModalProps) {
  const { token } = theme.useToken();
  return (
    <Modal
      open={open}
      width={ASK_WIDTH}
      title={
        <Space>
          <ExclamationCircleFilled style={{ color: token.colorWarning }} />
          {text.askAddTitle}
        </Space>
      }
      closable={{ 'aria-label': text.askClose }}
      onCancel={onClose}
      maskClosable={false}
      destroyOnHidden
      footer={
        <Space>
          <Button onClick={onSkip}>{text.askAddNo}</Button>
          <Button type="primary" onClick={onAdd}>
            {text.askAddYes}
          </Button>
        </Space>
      }
    >
      {fill(text.askAddBody, codes.length, codes.join(', '))}
    </Modal>
  );
}
