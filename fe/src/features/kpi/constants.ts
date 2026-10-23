/** Chuỗi hiển thị của màn KPI tổ may (UC19, MI-18 "KPI theo ngày"). */
export const KPI_TEXT = {
  adminPageTitle: 'KPI tổ may (xem)',
  adminPageSubtitle: 'Admin xem chỉ tiêu và kết quả thực tế của từng tổ may. Quản đốc là người lập, sửa, hủy chỉ tiêu.',
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
    period: 'Kỳ (từ - đến)',
    required: 'Vui lòng nhập thông tin này.',
    noProduct: 'Chọn đơn hàng trước.',
    dailyPlan: 'Chỉ tiêu từng ngày',
    dailyRequired: 'Vui lòng nhập chỉ tiêu cho từng ngày (mỗi ngày lớn hơn 0).',
  },

  daily: {
    pickPeriod: 'Chọn kỳ trước để nhập chỉ tiêu từng ngày.',
    date: 'Ngày',
    qty: 'Chỉ tiêu',
    remove: 'Bỏ ngày này',
    addDay: 'Thêm ngày (tăng ca)',
    rampTotal: 'Tổng muốn đạt cả kỳ',
    rampUp: 'Chia tăng dần',
    rampHint: 'Chia tăng dần: 3 ngày đầu 50% → 70% → 90% mức ổn định, các ngày sau 100%. Sửa tay từng ngày nếu cần.',
    total: 'Tổng cả kỳ',
    days: 'Số ngày làm việc',
    empty: 'Chưa có ngày nào trong kỳ.',
    weekdays: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
  },

  listPageTitle: 'Chỉ tiêu KPI tổ may',
  listPageSubtitle: 'Danh sách chỉ tiêu theo đơn hàng, mã hàng, tổ may và kỳ.',

  filter: {
    statusPlaceholder: 'Trạng thái',
  },

  table: {
    order: 'Đơn hàng',
    product: 'Mã hàng',
    sewingTeam: 'Tổ may',
    period: 'Kỳ (từ - đến)',
    targetFinishedQty: 'Chỉ tiêu cả kỳ',
    dailyTarget: 'Chỉ tiêu ngày (TB)',
    actual: 'Thực tế (hoàn thiện)',
    percent: 'Đạt (%)',
    status: 'Trạng thái',
    result: 'Kết quả',
    viewDetail: 'Xem theo ngày',
  },

  detail: {
    drawerTitlePrefix: 'Chỉ tiêu KPI',
    order: 'Đơn hàng',
    product: 'Mã hàng',
    sewingTeam: 'Tổ may',
    period: 'Kỳ',
    targetFinishedQty: 'Chỉ tiêu cả kỳ',
    workingDays: 'Số ngày làm việc',
    dailyTarget: 'Chỉ tiêu ngày (trung bình)',
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
