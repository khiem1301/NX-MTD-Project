/** Đơn đã bắt đầu giao: vẫn đặt được chỉ tiêu nhưng cần lưu ý. */
export const DELIVERING_ORDER_STATUSES: readonly (string | null | undefined)[] = [
  'InDelivery',
  'PartiallyDelivered',
];

/** Chuỗi hiển thị của màn KPI tổ may (UC19, MI-18 "KPI theo ngày"). */
export const KPI_TEXT = {
  add: 'Lập chỉ tiêu',
  edit: 'Sửa',
  cancel: 'Hủy chỉ tiêu',
  cancelConfirm: 'Hủy chỉ tiêu KPI này? Chỉ tiêu đã hủy không tính kết quả.',
  saved: 'Đã lưu chỉ tiêu KPI.',
  cancelled: 'Đã hủy chỉ tiêu KPI.',

  form: {
    addTitle: 'Lập chỉ tiêu KPI',
    editTitle: 'Sửa chỉ tiêu KPI',
    order: 'Đơn hàng',
    product: 'Mã hàng (theo đơn)',
    team: 'Tổ may',
    teamPickProduct: 'Chọn đơn hàng và mã hàng trước.',
    teamNotAllotted: 'Mã hàng chưa chia cho tổ nào. Chia ở màn Chia đơn cho tổ may.',
    period: 'Kỳ (từ - đến)',
    targetFinishedQty: 'Chỉ tiêu cả kỳ',
    required: 'Vui lòng nhập thông tin này.',
    qtyMin: 'Chỉ tiêu phải lớn hơn 0.',
    qtyMax: 'Chỉ tiêu tối đa 1.000.000.',
    periodMax: 'Kỳ chỉ tiêu tối đa 366 ngày.',
    noProduct: 'Chọn đơn hàng trước.',
    orderOption: '{0} (giao {1})',
    orderStatus: 'Trạng thái đơn',
    orderClosed: 'Đơn hàng đã dừng, đã hủy hoặc đã giao xong, không đặt chỉ tiêu được.',
    orderDelivering:
      'Đơn đang giao hàng, hãy kiểm tra còn cần may thêm không trước khi đặt chỉ tiêu.',
    dueDate: 'Ngày giao hàng',
    dueDaysLeft: 'Còn {0} ngày',
    dueOverdue: 'Quá hạn {0} ngày',
    dueToday: 'Giao hôm nay',
    dueAfter: 'Kỳ kết thúc sau ngày giao hàng {0} là {1} ngày, đơn có nguy cơ giao trễ.',
    dueNear: 'Kỳ kết thúc cách ngày giao hàng {0} chỉ {1} ngày, còn ít thời gian đóng gói và giao.',
    capacityOrderQty: 'Số lượng đơn đặt',
    capacityAssigned: 'Đã giao ở các chỉ tiêu khác',
    capacityMax: 'Tối đa lập được',
    capacityExceeds: 'Chỉ tiêu vượt số lượng đơn đặt, chưa lưu được.',
  },

  calendar: {
    button: 'KPI theo ngày',
    title: 'KPI theo ngày · {0} · {1} · {2} · {3} - {4}',
    monthTitle: 'Tháng {0}/{1}',
    prevMonth: 'Tháng trước',
    nextMonth: 'Tháng sau',
    kpi: 'KPI',
    dayInput: 'KPI ngày {0}',
    actual: 'Thực tế',
    total: 'Chỉ tiêu cả kỳ',
    finished: 'Đã hoàn thiện',
    allocated: 'Đã đặt / chưa đặt',
    allocatedValue: '{0} / {1}',
    btpAvailable: 'BTP khả dụng',
    btpDetail: 'Kho {0} + tổ giữ {1} − tổ khác cần {2}',
    btpLeft: 'BTP còn lại sau khi đặt',
    btpLeftHint: 'BTP khả dụng trừ KPI đã đặt chưa hoàn thiện',
    dueDate: 'Ngày giao hàng',
    dueDay: 'Ngày giao',
    afterDue: 'Có {0} ngày đặt KPI sau ngày giao hàng {1}, đơn có nguy cơ giao trễ.',
    overTotal: 'Tổng KPI các ngày vượt chỉ tiêu cả kỳ, chưa lưu được.',
    overBtp: 'KPI đã đặt chưa hoàn thiện vượt BTP khả dụng, chưa lưu được.',
    readOnly:
      'Chỉ xem: Quản đốc đặt KPI theo ngày khi chỉ tiêu còn đang theo dõi và đơn hàng chưa đóng.',
    pastLocked: 'Ngày đã qua không sửa được; KPI hôm nay chỉ sửa trước {0}.',
    reset: 'Hoàn tác',
    save: 'Lưu KPI theo ngày',
    saved: 'Đã lưu KPI theo ngày.',
  },

  listPageTitle: 'Chỉ tiêu KPI tổ may',
  listPageSubtitle:
    'Một đơn hàng, mã hàng và tổ may có thể có nhiều chỉ tiêu nhưng các kỳ không được trùng ngày; một đơn có thể chia cho nhiều tổ. Bấm KPI theo ngày để đặt trên lịch.',

  filter: {
    statusPlaceholder: 'Trạng thái',
  },

  table: {
    order: 'Đơn hàng',
    product: 'Mã hàng',
    sewingTeam: 'Tổ may',
    period: 'Kỳ (từ - đến)',
    progress: 'Tiến độ (thực tế / chỉ tiêu)',
    progressValue: '{0} / {1} ({2}%)',
    status: 'Trạng thái',
  },

  finish: {
    button: 'Kết thúc đơn',
    title: 'Kết thúc đơn của tổ',
    confirm: 'Kết thúc đơn',
    hint: 'Chỉ đánh giá khi đến cuối đơn. So sản lượng công nhân báo ở công đoạn cuối với số QC đạt và hàng hủy; phần lệch ghi thành phiếu hàng hỏng (trừ lương) cho từng công nhân, rồi chốt và đóng KPI.',
    target: 'Chỉ tiêu',
    actual: 'Thực tế (QC đạt)',
    result: 'Kết quả KPI',
    achieved: 'Đạt',
    notAchieved: 'Không đạt',
    shortfall: 'Thiếu',
    worker: 'Công nhân',
    line: 'Màu - size',
    reported: 'Báo ở công đoạn cuối',
    mismatch: 'Lệch (ghi lỗi)',
    mismatchTitle: 'Tổng {n} sản phẩm lệch sẽ ghi thành phiếu hàng hỏng',
    noMismatch: 'Không có lệch kiểm cuối đơn.',
    noOutput: 'Chưa có sản lượng báo ở công đoạn cuối.',
    otAuto:
      'KPI không đạt nhưng thiếu không quá {pct}%: nên tăng ca để bù; hệ thống báo Quản đốc và tổ trưởng.',
    otLeader:
      'KPI không đạt, thiếu hơn {pct}%: tổ trưởng của tổ quyết định có tăng ca hay không (hệ thống báo tổ trưởng).',
    done: 'Đã kết thúc đơn của tổ và chốt chỉ tiêu KPI.',
  },

  detail: {
    drawerTitlePrefix: 'Chỉ tiêu KPI',
    order: 'Đơn hàng',
    product: 'Mã hàng',
    sewingTeam: 'Tổ may',
    period: 'Kỳ',
    targetFinishedQty: 'Chỉ tiêu cả kỳ',
    workingDays: 'Số ngày làm việc',
    dailyTarget: 'Chỉ tiêu ngày',
    noDailyResultsHint:
      'Chỉ hiển thị các ngày QA/QC đã nhập hàng hoàn thiện. Ngày không có dòng nghĩa là ngày đó chưa có hàng hoàn thiện, không phải lỗi dữ liệu.',
  },

  dailyTable: {
    workDate: 'Ngày',
    dailyTarget: 'Chỉ tiêu ngày',
    finishedQty: 'Hoàn thiện',
    defectQty: 'Số lỗi',
    defectRatePct: 'Tỉ lệ lỗi',
    percentAchieved: 'Đạt (%)',
  },

  statusLabel: {
    Active: { text: 'Đang theo dõi', color: 'processing' },
    Closed: { text: 'Đã đóng', color: 'default' },
    Cancelled: { text: 'Đã hủy', color: 'red' },
  },

  resultLabel: {
    Achieved: { text: 'Đạt', color: 'green' },
    NotAchieved: { text: 'Không đạt', color: 'orange' },
  },
} as const;
