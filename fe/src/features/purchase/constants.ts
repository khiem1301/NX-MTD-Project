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
    /** Chỉ hiện khi đơn Chốt không bù: do Kế toán nhập khi xác nhận thanh toán (DEV-ALL-129). */
    actualTotal: 'Tổng giá trị thực tế',
    actualNotEntered: 'Chưa nhập (gợi ý {0} theo số đã nhận)',
    receivedQty: 'Đã nhận',
    notReplenishedQty: 'Không bù',
  },

  actions: {
    place: 'Chọn NCC và đặt hàng',
    cancel: 'Hủy đơn mua',
    reprice: 'Sửa đơn giá và gửi lại',
    confirmPayment: 'Xác nhận thanh toán',
    returnPayment: 'Trả lại Quản đốc',
    setActualAmount: 'Nhập giá trị thực tế',
    editOrder: 'Sửa đơn mua',
    editActualAmount: 'Sửa giá trị thực tế',
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
    hint: 'Kế toán đã trả lại đơn: chỉ sửa được đơn giá.',
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
    // DEV-ALL-129 / 185: đơn Chốt không bù — giá trị thực tế nhập riêng ở nút "Nhập giá trị thực tế", trạng thái xét theo giá trị này.
    actualAmount: 'Giá trị thực tế',
    proofTitle: 'Ảnh chứng từ lần thanh toán này',
    proofHint:
      'Bắt buộc có ít nhất một ảnh chứng từ mới cho mỗi lần thanh toán (tải lên trước khi xác nhận).',
    proofNew: 'Ảnh mới cho lần này: {0}',
    proofMissing: 'Chưa có ảnh chứng từ mới cho lần thanh toán này.',
    actualMissing: 'Đơn Chốt không bù: bấm "Nhập giá trị thực tế" trước khi thanh toán.',
    amountExceedsActual: 'Số tiền thanh toán không được lớn hơn tổng giá trị thực tế.',
    willBePaid: 'Trả đủ tổng giá trị thực tế: đơn sẽ là Đã thanh toán.',
    willBePartial: 'Trả ít hơn tổng giá trị thực tế: đơn sẽ là Thanh toán một phần.',
  },

  actual: {
    title: 'Nhập giá trị thực tế',
    hint: 'Nhập một lần. Sau lần nhập đầu tiên chỉ sửa được trong 24 giờ và phải ghi lý do. Tổng thanh toán không vượt số này.',
    amount: 'Giá trị thực tế',
    suggested: 'Theo số đã nhận: {0}. Tổng giá trị đơn: {1}.',
    paid: 'Đã thanh toán',
    reason: 'Lý do sửa',
    amountRequired: 'Nhập giá trị thực tế lớn hơn 0.',
    exceedsTotal: 'Giá trị thực tế không được lớn hơn tổng giá trị đơn mua.',
    reasonRequired: 'Vui lòng nhập lý do sửa.',
    belowPaid: 'Giá trị thực tế không được nhỏ hơn số đã thanh toán.',
  },

  edit: {
    title: 'Sửa đơn mua',
    hint: 'Sửa được trong 24 giờ kể từ lúc tạo, khi chưa nhận hàng và chưa thanh toán. Phải ghi lý do.',
    reason: 'Lý do sửa',
    reasonRequired: 'Vui lòng nhập lý do sửa.',
    supplierLocked: 'Nhà cung cấp không đổi được sau khi tạo đơn mua.',
    confirmTitle: 'Xác nhận sửa đơn mua?',
    confirmContent: 'Kiểm tra lại số lượng, đơn giá trước khi lưu.',
  },

  cancelTitle: 'Hủy đơn mua',
  returnTitle: 'Trả lại Quản đốc',
  saved: 'Đã lưu.',

  done: {
    placed: 'Đã đặt hàng. Kế toán sẽ xác nhận thanh toán.',
    cancelled: 'Đã hủy đơn mua.',
    repriced: 'Đã cập nhật đơn giá và gửi lại Kế toán.',
    paid: 'Đã ghi nhận lần thanh toán.',
    actualSaved: 'Đã lưu giá trị thực tế.',
    edited: 'Đã sửa đơn mua.',
    paymentUpdated: 'Đã sửa lần thanh toán.',
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
export const MAX_PROOF_PHOTOS = 20;
