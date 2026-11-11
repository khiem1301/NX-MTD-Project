import type {
  ClosedShortPurchase,
  IncomingCustomerMaterial,
  IncomingSupplierMaterial,
} from '../types';

/** Bộ lọc của một tab NVL chờ nhận; mỗi tab giữ bộ lọc riêng, ô nào không dùng thì để trống. */
export type IncomingFilter = {
  keyword: string;
  orderCode?: string;
  supplierName?: string;
  goodsStatus?: string;
};

export const EMPTY_INCOMING_FILTER: IncomingFilter = { keyword: '' };

type PurchaseRow = IncomingSupplierMaterial | ClosedShortPurchase;

/** Bỏ dấu + chữ thường để tìm không phân biệt hoa thường / dấu tiếng Việt. */
const normalize = (value: string | null | undefined) =>
  (value ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();

const matches = (keyword: string, fields: (string | null | undefined)[]) =>
  !keyword || fields.some((f) => normalize(f).includes(keyword));

/** Khách cấp: từ khoá khớp đơn hàng, mã / tên NVL, màu; có thể chọn một đơn hàng. */
export const filterCustomer = (rows: IncomingCustomerMaterial[], f: IncomingFilter) => {
  const keyword = normalize(f.keyword.trim());
  return rows.filter(
    (r) =>
      (!f.orderCode || r.orderCode === f.orderCode) &&
      matches(keyword, [r.orderCode, r.materialCode, r.materialName, r.materialColor]),
  );
};

/**
 * Đơn mua (chờ nhận hoặc đã đóng thiếu): nhà cung cấp và tình trạng lọc theo cả đơn; từ khoá khớp mã đơn mua, đơn hàng
 * hoặc bất kỳ NVL nào của đơn. Đơn khớp thì giữ đủ các dòng NVL (các dòng được gộp theo đơn ở bước sau).
 */
export const filterPurchase = <T extends PurchaseRow>(rows: T[], f: IncomingFilter): T[] => {
  const keyword = normalize(f.keyword.trim());
  const matched = new Set(
    rows
      .filter((r) =>
        matches(keyword, [
          r.poCode,
          r.orderCode,
          r.supplierName,
          r.materialCode,
          r.materialName,
          r.materialColor,
        ]),
      )
      .map((r) => r.purchaseOrderId),
  );
  return rows.filter(
    (r) =>
      matched.has(r.purchaseOrderId) &&
      (!f.supplierName || r.supplierName === f.supplierName) &&
      (!f.goodsStatus || ('goodsStatus' in r && r.goodsStatus === f.goodsStatus)),
  );
};

/** Giá trị khác nhau (bỏ rỗng, sắp theo chữ cái) làm lựa chọn cho ô chọn lọc. */
export const toFilterOptions = (values: (string | null | undefined)[]) =>
  [...new Set(values.filter((v): v is string => !!v))]
    .sort((a, b) => a.localeCompare(b))
    .map((value) => ({ value, label: value }));
