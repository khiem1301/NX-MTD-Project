import { formatCurrency } from '@/utils/format';
import { confirmDialog } from './confirmDialog';

/** Hỏi lại số tiền vừa nhập (hiện số lớn, có dấu chấm) trước khi gửi: nhập nhầm số 0 là lỗi hay gặp. */
export const confirmAmount = (label: string, amount: number, onOk: () => void) =>
  confirmDialog({
    title: `Xác nhận ${label.toLowerCase()}`,
    content: `${label}: ${formatCurrency(amount)}. Kiểm tra lại số tiền trước khi lưu.`,
    onOk,
  });
