import { createContext, useContext, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Alert, Descriptions, Modal, Space, Spin } from 'antd';
import type { DescriptionsProps } from 'antd';
import { MODAL_WIDTH } from './modalSize';

const ActionSlotContext = createContext<HTMLElement | null>(null);

type DetailModalProps = {
  open: boolean;
  onClose: () => void;
  /** Mã chứng từ / tên đối tượng đang xem. */
  title?: ReactNode;
  /** Cỡ Modal chuẩn (mặc định lg vì màn chi tiết thường có bảng). */
  size?: keyof typeof MODAL_WIDTH;
  /** Nút thao tác ở chân Modal (duyệt, sửa, hủy...). Nút nằm trong phần thân thì dùng `ModalActions`. */
  extra?: ReactNode;
  loading?: boolean;
  error?: { message: string } | null;
  children?: ReactNode;
};

/** Khung xem chi tiết dùng chung: Modal cùng cỡ, nút thao tác ở chân, cùng cách hiện loading / lỗi. */
export function DetailModal({ open, onClose, title, size = 'lg', extra, loading, error, children }: DetailModalProps) {
  const [slot, setSlot] = useState<HTMLElement | null>(null);
  return (
    <Modal
      open={open}
      onCancel={onClose}
      width={MODAL_WIDTH[size]}
      title={title}
      destroyOnHidden
      style={{ top: 24 }}
      styles={{ body: { maxHeight: 'calc(100vh - 180px)', overflowY: 'auto', paddingTop: 8 } }}
      footer={
        <Space wrap>
          {extra}
          <span ref={setSlot} />
        </Space>
      }
    >
      {loading && <Spin />}
      {error && <Alert type="error" message={error.message} />}
      <ActionSlotContext.Provider value={slot}>{children}</ActionSlotContext.Provider>
    </Modal>
  );
}

/**
 * Nút thao tác của màn chi tiết, luôn hiện ở chân Modal. Thứ tự quy ước:
 * nút phụ (in, sửa) → nút nguy hiểm (từ chối, hủy) → nút chính (xác nhận, duyệt) ngoài cùng bên phải.
 * Dùng khi nút cần ở trong phần thân (chung `form` với nội dung).
 */
export function ModalActions({ children }: { children: ReactNode }) {
  const slot = useContext(ActionSlotContext);
  return slot ? createPortal(<Space wrap>{children}</Space>, slot) : null;
}

/** Bảng thông tin chung của màn chi tiết: viền, cỡ nhỏ, 2 cột (1 cột trên điện thoại). */
export function DetailDescriptions(props: DescriptionsProps) {
  return <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} {...props} />;
}
