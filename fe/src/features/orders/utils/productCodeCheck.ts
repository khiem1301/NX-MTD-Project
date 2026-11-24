import { productApi } from '../api/orderApi';

/** Số mã hàng gần giống tối đa lấy về để so khớp chính xác (mã dài tối đa 30 ký tự, tìm theo từ khóa). */
const SEARCH_PAGE_SIZE = 50;

/**
 * Mã hàng đã có trong hệ thống chưa (không phân biệt hoa thường, kể cả mã đã ngừng sản xuất). Kiểm trước khi tạo mã mới ở form đơn
 * và màn Mã hàng để báo ngay ở ô mã, không đợi lưu. Không kiểm được (mất mạng, không đủ quyền) thì coi như chưa có: máy chủ vẫn
 * chặn mã trùng khi lưu.
 */
export const productCodeExists = async (code: string): Promise<boolean> => {
  const wanted = code.trim().toUpperCase();
  if (!wanted) return false;
  try {
    const found = await productApi.list({ page: 1, pageSize: SEARCH_PAGE_SIZE, keyword: wanted });
    return found.items.some((p) => p.productCode.trim().toUpperCase() === wanted);
  } catch {
    return false;
  }
};
