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
  discontinueTitle: 'Ngừng hợp tác (đơn mua cũ vẫn giữ nguyên). Lý do không bắt buộc',
  searchSupplier: 'Tìm theo tên, số điện thoại',
  searchOrder: 'Tìm theo mã đơn mua, mã đơn hàng, nhà cung cấp',
  allStatus: 'Tất cả trạng thái',
  allGoodsStatus: 'Mọi trạng thái đặt hàng',
  allPaymentStatus: 'Mọi trạng thái thanh toán',

  supplierFields: {
    name: 'Tên nhà cung cấp',
    nameRequired: 'Vui lòng nhập tên nhà cung cấp.',
    phone: 'Số điện thoại',
    email: 'Email',
    emailInvalid: 'Email không hợp lệ.',
    address: 'Địa chỉ',
    materialTypes: 'Loại NVL cung cấp',
    note: 'Ghi chú',
    status: 'Trạng thái',
    discontinueReason: 'Lý do ngừng hợp tác',
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
    remaining: 'Còn phải thanh toán',
    enteredAt: 'Nhập lúc',
    editedNote: 'Đã sửa',
    editPayment: 'Sửa lần thanh toán',
    actualOverrideReason: 'Lý do nhập giá trị thực tế',
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
    /** Giá trị đơn đã trừ phần chốt không bù (hoặc số Kế toán nhập); tổng các lần thanh toán không được vượt quá. */
    actualTotal: 'Tổng giá trị thực tế',
  },

  actions: {
    place: 'Chọn NCC và đặt hàng',
    cancel: 'Hủy đơn mua',
    reprice: 'Sửa đơn giá và gửi lại',
    setActual: 'Nhập giá trị thực tế',
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
    expectedDeliveryDate: 'Ngày nhà cung cấp dự kiến giao',
    expectedDeliveryDateRequired: 'Vui lòng chọn ngày dự kiến giao.',
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
    title: 'Thêm lần thanh toán đơn mua',
    editTitle: 'Sửa lần thanh toán',
    editReason: 'Lý do sửa',
    editReasonRequired: 'Vui lòng nhập lý do sửa.',
    maxForThis: 'Tối đa cho lần này',
    remaining: 'Còn phải thanh toán',
    date: 'Ngày thanh toán',
    amount: 'Số tiền thanh toán',
    method: 'Hình thức',
    note: 'Ghi chú',
    differsFromTotal:
      'Số tiền ít hơn số còn phải thanh toán: đơn sẽ ở trạng thái thanh toán một phần.',
    amountExceedsTotal:
      'Số tiền thanh toán không được lớn hơn số còn phải thanh toán (giá trị thực tế trừ số đã thanh toán).',
    dateRequired: 'Vui lòng chọn ngày thanh toán.',
    amountRequired: 'Nhập số tiền lớn hơn 0.',
    methodRequired: 'Vui lòng chọn hình thức.',
  },

  actual: {
    title: 'Nhập giá trị thực tế',
    hint: 'Dùng khi NCC giao thiếu hoặc đổi giá: tổng các lần thanh toán không vượt quá số này. Phải có lý do.',
    amount: 'Giá trị thực tế',
    computed: 'Giá trị tự tính',
    paid: 'Đã thanh toán',
    reason: 'Lý do',
    amountRequired: 'Nhập giá trị thực tế.',
    reasonRequired: 'Vui lòng nhập lý do.',
    belowPaid: 'Giá trị thực tế không được nhỏ hơn số đã thanh toán.',
    clear: 'Bỏ giá trị đã nhập (dùng giá trị tự tính)',
  },

  cancelTitle: 'Hủy đơn mua',
  returnTitle: 'Trả lại Quản đốc',
  saved: 'Đã lưu.',

  done: {
    placed: 'Đã đặt hàng. Kế toán sẽ xác nhận thanh toán.',
    cancelled: 'Đã hủy đơn mua.',
    repriced: 'Đã cập nhật đơn giá và gửi lại Kế toán.',
    paid: 'Đã ghi nhận lần thanh toán.',
    paymentUpdated: 'Đã sửa lần thanh toán.',
    actualSaved: 'Đã lưu giá trị thực tế.',
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
  PartiallyPaid: { text: 'Thanh toán một phần', color: 'cyan' },
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
