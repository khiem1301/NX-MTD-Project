import type { FormInstance } from 'antd';
import type { NamePath } from 'antd/es/form/interface';

/**
 * Kiểm lại ngay các ô phụ thuộc vào ô vừa đổi (VD lý do lệch khi đổi số nhận). Ant Design chỉ kiểm lại ô phụ thuộc khi ô đó
 * đã từng được chạm vào, nên không gọi thì lỗi chỉ hiện lúc bấm Lưu thay vì ngay dưới ô khi đang nhập.
 */
export const revalidate = (form: FormInstance, ...names: NamePath[]): void => {
  form.validateFields(names).catch(() => undefined);
};
