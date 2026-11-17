import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { orderLookupApi } from '@/services/orderLookupApi';
import { orderImportApi, productImportApi, specImportApi } from '../api/orderApi';
import type { SpecImportTarget } from '../types';
import { ORDERS_KEY } from './useOrders';

/** Số đơn tối đa hiện trong ô chọn đơn (đơn đang chạy của một mã hàng). */
const PRODUCT_ORDERS_PAGE_SIZE = 100;

/** DEV-ALL-115: xem trước file Excel định mức — không ghi gì nên không làm mới dữ liệu nào. */
export const usePreviewSpecImport = () =>
  useApiMutation(
    ({ target, file }: { target: SpecImportTarget; file: File }) =>
      specImportApi.preview(target, file),
    { invalidate: [] },
  );

/**
 * Nhập định mức + bìa của đơn: định mức đổi thì đơn hàng, NVL của đơn, tra cứu và lệnh cắt (ước vải theo bìa) cũng phải tải lại.
 * Thông báo kết quả lấy từ máy chủ (số dòng đã nhập) nên không đặt successMessage ở đây.
 */
export const useSpecImport = () =>
  useApiMutation(
    ({ target, file }: { target: SpecImportTarget; file: File }) =>
      specImportApi.import(target, file),
    { invalidate: [ORDERS_KEY, ['lookups'], ['order-lookups'], ['warehouse'], ['cutting']] },
  );

/** Đơn đang chạy có mã hàng này, để chọn đơn nhận bìa khi nhập ở màn mã hàng. */
export const useProductOrders = (productId: number) =>
  useQuery({
    queryKey: ['order-lookups', 'orders', 'by-product', productId],
    queryFn: () => orderLookupApi.orders({ productId, pageSize: PRODUCT_ORDERS_PAGE_SIZE }),
  });

/** DEV-ALL-118: xem trước dòng hàng của đơn từ Excel — không ghi gì. */
export const usePreviewOrderImport = () =>
  useApiMutation((file: File) => orderImportApi.preview(file), { invalidate: [] });

/** DEV-ALL-120: xem trước file Excel mã hàng — không ghi gì. */
export const usePreviewProductImport = () =>
  useApiMutation((file: File) => productImportApi.preview(file), { invalidate: [] });

/** Tạo mã hàng từ Excel: làm mới danh sách mã hàng và ô chọn mã hàng của form đơn. */
export const useProductImport = () =>
  useApiMutation((file: File) => productImportApi.import(file), {
    invalidate: [ORDERS_KEY, ['order-lookups'], ['lookups']],
  });
