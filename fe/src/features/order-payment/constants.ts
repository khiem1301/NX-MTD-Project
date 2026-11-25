import type { StatusLabel } from '@/components/common/StatusTag';
import type { OrderPaymentMethod, OrderPaymentState } from './types';

export const ORDER_PAYMENT_STATE_LABEL: Record<OrderPaymentState, StatusLabel> = {
  NotSet: { text: 'Chưa chốt giá trị', color: 'default' },
  Unpaid: { text: 'Chưa thanh toán', color: 'gold' },
  Partial: { text: 'Thanh toán một phần', color: 'blue' },
  Settled: { text: 'Đã tất toán', color: 'green' },
};

export const ORDER_PAYMENT_STATE_OPTIONS = (
  Object.keys(ORDER_PAYMENT_STATE_LABEL) as OrderPaymentState[]
).map((value) => ({ value, label: ORDER_PAYMENT_STATE_LABEL[value].text }));

export const ORDER_PAYMENT_METHOD_LABEL: Record<OrderPaymentMethod, string> = {
  BankTransfer: 'Chuyển khoản',
  Cash: 'Tiền mặt',
};

export const ORDER_PAYMENT_METHOD_OPTIONS = (
  Object.keys(ORDER_PAYMENT_METHOD_LABEL) as OrderPaymentMethod[]
).map((value) => ({ value, label: ORDER_PAYMENT_METHOD_LABEL[value] }));

/** Chữ hiển thị của màn Công nợ đơn hàng (DEV-ALL-137; đổi tên ở DEV-ALL-165). */
export const ORDER_PAYMENT_TEXT = {
  title: 'Công nợ đơn hàng',
  subtitle:
    'Chốt giá trị đơn hàng (có thể âm khi phải đền hợp đồng) và ghi nhận các lần thanh toán. Số dương: khách trả; số âm: mình trả khách.',
  search: 'Tìm theo mã đơn hoặc khách hàng',
  allStates: 'Mọi trạng thái',
  columns: {
    order: 'Đơn hàng',
    customer: 'Khách hàng',
    orderAmount: 'Tổng tiền đơn',
    finalAmount: 'Giá trị chốt',
    paid: 'Đã thanh toán',
    remaining: 'Còn lại',
    state: 'Thanh toán',
  },
  detail: {
    finalAmount: 'Giá trị chốt',
    finalReason: 'Lý do chốt / đổi giá trị',
    notSet: 'Chưa chốt',
    paid: 'Đã thanh toán',
    remaining: 'Còn lại',
    customerOwes: 'Khách còn phải trả',
    weOwe: 'Mình còn phải trả khách',
    settled: 'Đã tất toán',
    payments: 'Các lần thanh toán',
    noPayments: 'Chưa có lần thanh toán nào.',
    needFinal: 'Chốt giá trị đơn hàng trước khi ghi nhận thanh toán.',
    history: 'Lịch sử thay đổi',
    enteredAt: 'Nhập lúc',
    edited: 'Đã sửa',
  },
  actions: {
    setFinal: 'Nhập giá trị thực tế',
    editFinal: 'Sửa giá trị thực tế',
    addPayment: 'Thêm lần thanh toán',
    editPayment: 'Sửa lần thanh toán',
  },
  final: {
    title: 'Giá trị thực tế của đơn hàng',
    amount: 'Giá trị thực tế',
    amountHint:
      'Nhập một lần; sau lần nhập đầu tiên chỉ sửa được trong 24 giờ và phải ghi lý do. Dương: khách trả mình. Âm: mình đền khách. Tổng đã thanh toán phải nằm giữa 0 và giá trị này.',
    amountRequired: 'Nhập giá trị thực tế.',
    reason: 'Lý do sửa',
    reasonRequired: 'Vui lòng nhập lý do sửa.',
  },
  pay: {
    addTitle: 'Thêm lần thanh toán',
    editTitle: 'Sửa lần thanh toán',
    date: 'Ngày thanh toán',
    dateRequired: 'Vui lòng chọn ngày thanh toán.',
    amount: 'Số tiền',
    amountHint:
      'Dương: khách trả mình. Âm: mình trả khách. Tổng các lần nằm giữa 0 và giá trị chốt.',
    amountRequired: 'Nhập số tiền khác 0.',
    allowed: 'Còn nhập được',
    method: 'Hình thức',
    methodRequired: 'Vui lòng chọn hình thức.',
    note: 'Ghi chú',
    editReason: 'Lý do sửa',
    editReasonRequired: 'Vui lòng nhập lý do sửa.',
    outOfRange: 'Tổng đã thanh toán phải nằm giữa 0 và giá trị chốt của đơn hàng.',
  },
  done: {
    finalSaved: 'Đã lưu giá trị chốt.',
    added: 'Đã ghi nhận lần thanh toán.',
    updated: 'Đã sửa lần thanh toán.',
  },
} as const;
