import { cloneElement, type MouseEvent, type ReactElement, type ReactNode } from 'react';
import { confirmDialog } from '@/utils/confirmDialog';

type ConfirmActionProps = {
  title: ReactNode;
  description?: ReactNode;
  /** Trả Promise thì hộp xác nhận hiện trạng thái đang xử lý đến khi xong. */
  onConfirm: () => void | Promise<unknown>;
  okText?: string;
  cancelText?: string;
  /** Nút bấm không mở hộp xác nhận khi true. */
  disabled?: boolean;
  /** Một phần tử bấm được (nút, liên kết); bấm vào thì mở hộp xác nhận ở giữa màn hình. */
  children: ReactElement<{ onClick?: (event: MouseEvent<HTMLElement>) => void }>;
};

/**
 * Thao tác cần xác nhận lại: bấm vào thì hiện hộp thoại xác nhận (cùng kiểu với `confirmDialog`) rồi mới chạy `onConfirm`.
 * Dùng thay cho Popconfirm để mọi chỗ xác nhận trong hệ thống có cùng một giao diện.
 */
export function ConfirmAction({
  title,
  description,
  onConfirm,
  okText,
  cancelText,
  disabled = false,
  children,
}: ConfirmActionProps) {
  const open = (event: MouseEvent<HTMLElement>) => {
    event.stopPropagation();
    if (disabled) return;
    // Không truyền okText / cancelText undefined để giữ nhãn mặc định của confirmDialog.
    confirmDialog({
      title,
      content: description,
      onOk: onConfirm,
      ...(okText && { okText }),
      ...(cancelText && { cancelText }),
    });
  };
  return cloneElement(children, { onClick: open });
}
