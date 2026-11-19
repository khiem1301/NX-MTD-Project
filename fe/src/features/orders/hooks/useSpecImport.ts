import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { orderLookupApi } from '@/services/orderLookupApi';
import { orderImportApi, specImportApi } from '../api/orderApi';
import type { SpecImportTarget } from '../types';
import { ORDERS_KEY } from './useOrders';

/** Số đơn tối đa hiện trong ô chọn đơn (đơn đang chạy của một mã hàng). */
const PRODUCT_ORDERS_PAGE_SIZE = 100;

/** DEV-ALL-115: xem trước file Excel định mức — không ghi gì nên không làm mới dữ liệu nào. */
export const usePreviewSpecImport = () =>
  useApiMutation(
    ({ target, file, replace }: { target: SpecImportTarget; file: File; replace?: boolean }) =>
      specImportApi.preview(target, file, replace),
    { invalidate: [] },
  );

/**
 * Nhập định mức + bìa của đơn: định mức đổi thì đơn hàng, NVL của đơn, tra cứu và lệnh cắt (ước vải theo bìa) cũng phải tải lại.
 * Thông báo kết quả lấy từ máy chủ (số dòng đã nhập) nên không đặt successMessage ở đây.
 */
export const useSpecImport = () =>
  useApiMutation(
    ({ target, file, replace }: { target: SpecImportTarget; file: File; replace?: boolean }) =>
      specImportApi.import(target, file, replace),
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
