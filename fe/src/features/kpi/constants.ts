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
    targetFinishedQty: 'Chỉ tiêu cả kỳ',
    required: 'Vui lòng nhập thông tin này.',
    qtyMin: 'Chỉ tiêu phải lớn hơn 0.',
    noProduct: 'Chọn đơn hàng trước.',
    dailyHint: 'Chỉ tiêu từng ngày đặt sau khi lập, ở nút "Xem theo ngày" (lịch KPI).',
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
      'Hiển thị mọi ngày có chỉ tiêu (chưa có hàng hoàn thiện thì số hoàn thiện = 0) và các ngày QA/QC nhập hàng ngoài kế hoạch (không có chỉ tiêu ngày).',
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

/** Lịch KPI theo ngày: đặt KPI từng ngày, Tổng bán thành phẩm và số còn lại để đặt KPI. */
export const CAL_TEXT = {
  title: 'Lịch KPI theo ngày',
  weekdays: ['Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy', 'Chủ nhật'],
  monthTitle: (month: number, year: number) => `THÁNG ${month} NĂM ${year}`,
  prevMonth: 'Tháng trước',
  nextMonth: 'Tháng sau',
  sum: {
    plan: 'Chỉ tiêu cả kỳ',
    allocated: 'Đã chia cho các ngày',
    unallocated: 'Chưa chia',
    totalBtp: 'Tổng bán thành phẩm',
    totalBtpHint: (cut: number, out: number) => `Đã cắt ${cut} − đã ra chuyền ${out}`,
    committed: 'KPI đã đặt chưa ra chuyền',
    remaining: 'Số lượng còn lại để đặt KPI',
    remainingHint: 'Tổng bán thành phẩm − KPI đã đặt chưa ra chuyền (mọi tổ cùng đơn và mã hàng). Giảm khi QA/QC nhập hàng ra chuyền.',
  },
  cell: {
    actual: 'Thực tế',
    btp: 'BTP còn',
    cut: 'Cắt',
    low: 'Sắp thiếu BTP',
    behind: 'Chậm KPI',
    future: 'Dự báo',
  },
  tool: {
    ramp: 'Chia tăng dần',
    even: 'Chia đều',
    clear: 'Xóa các ngày chưa tới',
    save: 'Lưu KPI theo ngày',
    reset: 'Bỏ thay đổi',
    rampHint: 'Chia phần chỉ tiêu còn lại (cả kỳ trừ số đã làm) cho các ngày từ hôm nay đến hết kỳ, bỏ Chủ nhật; 3 ngày đầu 50% → 70% → 90% mức ổn định. Sau đó sửa tay từng ngày.',
  },
  saved: 'Đã lưu KPI theo ngày.',
  overPlan: 'Tổng KPI các ngày vượt chỉ tiêu cả kỳ.',
  overBtp: 'KPI đã đặt vượt số bán thành phẩm còn lại.',
  readOnly: 'Chỉ xem (chỉ Quản đốc đặt KPI theo ngày, và chỉ khi KPI còn đang chạy).',
  pastLocked: 'Ngày đã qua không sửa được.',
} as const;
