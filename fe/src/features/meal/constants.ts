/** Chuỗi hiển thị của luồng suất ăn (UC30). Thông báo kết quả thao tác nằm ở constants/messages.ts. */
export const MEAL_TEXT = {
  teamPageTitle: 'Suất ăn của tổ',
  teamPageSubtitle:
    'Người đã quẹt vân tay được tick sẵn. Bỏ tick hộ nếu công nhân không ăn; xác nhận trước giờ chốt.',
  myPageTitle: 'Suất ăn của tôi',
  myPageSubtitle: 'Bỏ tick nếu hôm nay bạn không ăn. Chỉ hiện khi đã quẹt vân tay.',
  settingsPageTitle: 'Giờ chốt suất ăn',
  settings: {
    subtitle:
      'Admin cấu hình giờ nhắc tổ trưởng xác nhận và giờ hệ thống tự chốt suất ăn mỗi ngày.',
    hint: 'Sau giờ chốt, không sửa được suất ăn và suất khách. Đổi giờ có hiệu lực ngay từ lần kiểm tra kế tiếp; ngày đã chốt rồi thì không mở lại.',
    reminder: 'Giờ nhắc xác nhận',
    reminderHelp:
      'Tổ chưa xác nhận lúc này sẽ được nhắc; hệ thống tạm lấy theo số suất đang tick. Mặc định 08:15.',
    cutoff: 'Giờ chốt suất ăn',
    cutoffHelp: 'Từ {from} đến {to}. Mặc định 08:30.',
    required: 'Vui lòng chọn giờ.',
    order: 'Giờ nhắc phải sớm hơn giờ chốt.',
    cutoffRange: 'Giờ chốt phải từ {from} đến {to}.',
    save: 'Lưu',
  },
  summaryPageTitle: 'Suất ăn hôm nay',
  summaryPageSubtitle: 'Nhập suất khách, xem tổng hợp theo tổ và nhập đơn giá suất ăn.',

  tabs: {
    guest: 'Suất khách',
    summary: 'Tổng hợp',
    price: 'Đơn giá',
  },

  my: {
    willEatLabel: 'Tôi ăn hôm nay',
    noAttendance: 'Bạn chưa quẹt vân tay hôm nay nên chưa có suất ăn.',
    punchedAfterLock:
      'Bạn đã quẹt vân tay, nhưng suất ăn đã chốt trước đó (quẹt sau giờ chốt hoặc sau khi hệ thống tự chốt) nên không còn suất / không đổi được.',
  },

  checklist: {
    code: 'Mã NV',
    name: 'Họ tên',
    willEat: 'Suất ăn',
    attendanceCount: 'Đã quẹt',
    mealCount: 'Còn tick',
    hint: 'Chỉ hiện người đã quẹt. Bỏ tick nếu không ăn; có thể tick lại trước giờ chốt.',
    empty: 'Chưa có ai trong tổ quẹt vân tay hôm nay.',
  },

  suggestion: {
    suggestedCount: 'Số gợi ý theo chấm công',
    mealCount: 'Số suất ăn của tổ',
    confirm: 'Xác nhận đã rà soát',
    confirmedAt: 'Xác nhận lúc',
    lockedHint: 'Đã chốt suất ăn, không thể thay đổi.',
    sourceConfirmed: 'Đã xác nhận',
    sourceAttendance: 'Theo chấm công',
    sourceNone: 'Chưa xác nhận',
  },

  guestForm: {
    date: 'Ngày',
    guestCount: 'Số suất khách',
    guestNote: 'Ghi chú (khách của ai, bộ phận nào...)',
    save: 'Lưu suất khách',
    lockedHint: 'Ngày này đã chốt, không thể sửa suất khách.',
  },

  summary: {
    status: 'Trạng thái',
    team: 'Tổ',
    mealCount: 'Số suất',
    attendanceCount: 'Số đã quẹt',
    source: 'Nguồn',
    officeCount: 'Văn phòng',
    guestCount: 'Suất khách',
    teamTotal: 'Tổng suất tổ',
    totalMeals: 'Tổng cộng',
    attendanceIncomplete: 'Thiếu dữ liệu chấm công lúc chốt',
    notifyKitchen: 'Báo bếp',
    kitchenNotifiedAt: 'Đã báo bếp lúc',
    notifyDisabledHint: 'Chỉ báo bếp được sau khi đã chốt suất ăn.',
  },

  priceForm: {
    totalMeals: 'Tổng số suất đã chốt',
    mealPrice: 'Đơn giá một suất',
    totalAmount: 'Thành tiền',
    save: 'Lưu đơn giá',
    disabledHint: 'Chỉ nhập đơn giá sau khi ngày này đã chốt suất ăn.',
  },

  statusLabel: {
    Open: 'Đang mở',
    AutoClosed: 'Đã chốt tự động',
    Closed: 'Đã chốt',
    KitchenNotified: 'Đã báo bếp',
  },
} as const;
