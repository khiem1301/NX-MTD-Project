import type { StatusLabel } from '@/components/common/StatusTag';
import type { PaymentMethod, PoGoodsStatus, PoPaymentStatus, SupplierStatus } from './types';

/** Chuỗi hiển thị của luồng nhà cung cấp, đơn mua, thanh toán (MI-07). Lỗi nghiệp vụ lấy message từ BE. */
export const PURCHASE_TEXT = {
  suppliersTitle: 'Nhà cung cấp',
  suppliersSubtitle:
    'Danh sách nhà cung cấp NVL. Quản đốc chỉ chọn được nhà cung cấp đang hợp tác khi đặt hàng.',
  ordersTitle: 'Đơn mua NVL',
  createOrder: 'Tạo đơn mua',
  ordersSubtitle: 'Theo dõi đơn mua: chọn nhà cung cấp và đặt hàng, hoặc hủy đơn chưa đặt.',
  paymentsTitle: 'Thanh toán đơn mua',
  paymentsSubtitle:
    'Đơn mua đã đặt hàng: xác nhận thanh toán (có thể kèm ảnh chứng từ) hoặc trả lại Quản đốc.',

  add: 'Thêm nhà cung cấp',
  editSupplier: 'Sửa nhà cung cấp',
  discontinue: 'Ngừng hợp tác',
  reactivate: 'Hợp tác lại',
  discontinueConfirm: 'Ngừng hợp tác với nhà cung cấp này? Đơn mua cũ vẫn giữ nguyên.',
  searchSupplier: 'Tìm theo tên, số điện thoại',
  searchOrder: 'Tìm theo mã đơn mua, mã đơn hàng, nhà cung cấp',
  allStatus: 'Tất cả trạng thái',
  allGoodsStatus: 'Mọi trạng thái đặt hàng',
  allPaymentStatus: 'Mọi trạng thái thanh toán',

  supplierFields: {
    name: 'Tên nhà cung cấp',
    nameRequired: 'Vui lòng nhập tên nhà cung cấp.',
    phone: 'Số điện thoại',
    phoneInvalid: 'Chỉ gồm chữ số và dấu +.',
    email: 'Email',
    emailInvalid: 'Email không hợp lệ.',
    address: 'Địa chỉ',
    materialTypes: 'Loại NVL cung cấp',
    note: 'Ghi chú',
    status: 'Trạng thái',
    orderCount: 'Số đơn mua',
  },

  columns: {
    poCode: 'Mã đơn mua',
    order: 'Đơn hàng',
    stock: 'Mua dự trữ',
    supplier: 'Nhà cung cấp',
    goodsStatus: 'Đặt hàng',
    paymentStatus: 'Thanh toán',
    orderDate: 'Ngày đặt',
    total: 'Giá trị đơn',
    paid: 'Đã thanh toán',
  },

  detail: {
    expectedDate: 'Lần giao (đã nhận / dự kiến)',
    note: 'Ghi chú',
    rejectReason: 'Lý do hủy đơn',
    returnReason: 'Lý do trả lại',
    paymentInfo: 'Thanh toán',
    paymentDate: 'Ngày thanh toán',
    paidAmount: 'Số tiền đã thanh toán',
    paymentMethod: 'Hình thức',
    paymentNote: 'Ghi chú thanh toán',
    lines: 'Dòng đơn mua',
    proofs: 'Ảnh chứng từ thanh toán',
    proofsHint: 'Ảnh chứng từ không bắt buộc, tối đa 5 ảnh.',
    history: 'Lịch sử thay đổi',
    material: 'NVL',
    color: 'Màu',
    qty: 'Số lượng',
    unitPrice: 'Đơn giá',
    amount: 'Thành tiền',
    total: 'Tổng giá trị',
  },

  actions: {
    place: 'Chọn NCC và đặt hàng',
    cancel: 'Hủy đơn mua',
    reprice: 'Sửa đơn giá và gửi lại',
    confirmPayment: 'Xác nhận thanh toán',
    returnPayment: 'Trả lại Quản đốc',
  },

  place: {
    title: 'Chọn nhà cung cấp và đặt hàng',
    supplier: 'Nhà cung cấp',
    supplierRequired: 'Vui lòng chọn nhà cung cấp.',
    orderDate: 'Ngày đặt hàng',
    orderDateRequired: 'Vui lòng chọn ngày đặt hàng.',
    expectedDate: 'Số lần nhà cung cấp dự kiến giao',
    expectedDateRequired: 'Nhập số lần giao dự kiến (từ 1 trở lên).',
    note: 'Ghi chú',
    priceRequired: 'Nhập đơn giá lớn hơn 0.',
    qtyRequired: 'Nhập số lượng lớn hơn 0.',
    lockedHint: 'Sau khi đặt hàng, số lượng và nhà cung cấp bị khóa.',
    noSupplier: 'Chưa có nhà cung cấp nào đang hợp tác. Nhờ Admin thêm trước.',
  },

  reprice: {
    title: 'Sửa đơn giá và gửi lại Kế toán',
    hint: 'Kế toán đã trả lại đơn. Chỉ sửa được đơn giá; số lượng và nhà cung cấp giữ nguyên.',
    returnedBy: 'Lý do Kế toán trả lại',
  },

  pay: {
    title: 'Xác nhận thanh toán đơn mua',
    date: 'Ngày thanh toán',
    amount: 'Số tiền thanh toán',
    method: 'Hình thức',
    note: 'Ghi chú',
    differsFromTotal: 'Số tiền khác giá trị đơn mua.',
    dateRequired: 'Vui lòng chọn ngày thanh toán.',
    amountRequired: 'Nhập số tiền lớn hơn 0.',
    methodRequired: 'Vui lòng chọn hình thức.',
  },

  cancelTitle: 'Hủy đơn mua',
  returnTitle: 'Trả lại Quản đốc',
  saved: 'Đã lưu.',

  done: {
    placed: 'Đã đặt hàng. Kế toán sẽ xác nhận thanh toán.',
    cancelled: 'Đã hủy đơn mua.',
    repriced: 'Đã cập nhật đơn giá và gửi lại Kế toán.',
    paid: 'Đã xác nhận thanh toán đơn mua.',
    returned: 'Đã trả lại Quản đốc kèm lý do.',
  },
} as const;

export const SUPPLIER_STATUS_LABEL: Record<SupplierStatus, StatusLabel> = {
  Active: { text: 'Đang hợp tác', color: 'green' },
  Discontinued: { text: 'Ngừng hợp tác', color: 'default' },
};

export const GOODS_STATUS_LABEL: Record<PoGoodsStatus, StatusLabel> = {
  PendingFinalisation: { text: 'Chờ đặt hàng', color: 'gold' },
  Ordered: { text: 'Đã đặt hàng', color: 'blue' },
  PartiallyReceived: { text: 'Đã nhận một phần', color: 'cyan' },
  Received: { text: 'Đã nhận đủ', color: 'green' },
  ClosedShort: { text: 'Chốt không bù', color: 'orange' },
  CustomerSupplied: { text: 'Khách cấp', color: 'default' },
  Rejected: { text: 'Đã hủy', color: 'red' },
};

export const PAYMENT_STATUS_LABEL: Record<PoPaymentStatus, StatusLabel> = {
  Unpaid: { text: 'Chưa thanh toán', color: 'gold' },
  Paid: { text: 'Đã thanh toán', color: 'green' },
  Returned: { text: 'Bị trả lại', color: 'red' },
};

export const PAYMENT_METHOD_LABEL: Record<PaymentMethod, string> = {
  BankTransfer: 'Chuyển khoản',
  Cash: 'Tiền mặt',
};

const toOptions = <K extends string>(labels: Record<K, string>) =>
  (Object.keys(labels) as K[]).map((value) => ({ value, label: labels[value] }));

const toStatusOptions = <K extends string>(labels: Record<K, StatusLabel>) =>
  (Object.keys(labels) as K[]).map((value) => ({ value, label: labels[value].text }));

export const PAYMENT_METHOD_OPTIONS = toOptions(PAYMENT_METHOD_LABEL);
export const GOODS_STATUS_OPTIONS = toStatusOptions(GOODS_STATUS_LABEL);
export const PAYMENT_STATUS_OPTIONS = toStatusOptions(PAYMENT_STATUS_LABEL);
export const SUPPLIER_STATUS_OPTIONS = toStatusOptions(SUPPLIER_STATUS_LABEL);

/** Tối đa ảnh chứng từ / đơn mua (khớp PurchaseOrderAttachmentAccess ở BE). */
export const MAX_PROOF_PHOTOS = 5;
