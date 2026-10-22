/** Chuỗi hiển thị của màn hình chấm công (Rules FE: chuỗi gom một nơi). */
export const ATTENDANCE_TEXT = {
  pageTitle: 'Chấm công',
  pageSubtitle: 'Dữ liệu lấy từ máy vân tay Ronald Jack Pro',
  myPageTitle: 'Ngày công của tôi',
  teamPageTitle: 'Chấm công tổ',

  tabs: {
    timesheet: 'Bảng công ngày',
    unmapped: 'Mã chưa gán',
    sessions: 'Lịch sử đồng bộ',
    adjustments: 'Chấm công hộ',
    import: 'Import Excel dự phòng',
  },

  adjust: {
    title: 'Chấm công hộ',
    hint: 'Chấm bù cho nhân viên quên quẹt, vân tay lỗi, đi làm ngoài hoặc theo đơn xin chấm bù đã được duyệt. Bắt buộc ghi lý do và có ít nhất giờ vào hoặc giờ ra.',
    add: 'Chấm công hộ',
    addTitle: 'Chấm công hộ cho nhân viên',
    team: 'Tổ',
    employee: 'Nhân viên',
    workDate: 'Ngày công',
    type: 'Loại chấm bù',
    reason: 'Lý do',
    request: 'Đơn xin chấm bù đã duyệt',
    requestRequired: 'Chọn đơn xin chấm bù đã được duyệt.',
    checkIn: 'Giờ vào',
    checkOut: 'Giờ ra',
    required: 'Vui lòng nhập thông tin này.',
    needTime: 'Nhập giờ vào hoặc giờ ra.',
    saved: 'Đã chấm công hộ.',
    period: 'Khoảng ngày',
    search: 'Tìm theo mã, tên nhân viên',
    empty: 'Chưa có lượt chấm công hộ nào trong khoảng ngày này.',
    columns: { date: 'Ngày công', employee: 'Nhân viên', type: 'Loại', reason: 'Lý do', times: 'Giờ vào - ra', request: 'Đơn', recordedAt: 'Ghi lúc' },
    types: {
      FORGOT_PUNCH: 'Quên quẹt thẻ',
      FINGERPRINT_FAIL: 'Vân tay lỗi',
      OFFSITE_WORK: 'Đi làm ngoài',
      APPROVED_REQUEST: 'Theo đơn xin chấm bù',
    } as Record<string, string>,
    approvedRequests: {
      title: 'Đơn xin chấm công bù đã duyệt, chờ chấm hộ',
      empty: 'Không có đơn xin chấm công bù nào đang chờ.',
      columns: { code: 'Mã đơn', title: 'Tiêu đề', team: 'Tổ', creator: 'Người tạo', date: 'Ngày xin bù', action: 'Chấm hộ' },
    },
  },

  stats: {
    employees: 'Số nhân viên',
    workdays: 'Tổng ngày công',
    overtime: 'Tổng giờ tăng ca',
    missingPunch: 'Thiếu lượt quẹt',
    unmappedCodes: 'Mã chưa gán',
    lastSync: 'Đồng bộ gần nhất',
  },

  filter: {
    dateRange: 'Khoảng ngày',
    team: 'Tổ',
    allTeams: 'Tất cả tổ',
    keyword: 'Tìm theo tên hoặc mã nhân viên',
    onlyMissing: 'Chỉ dòng thiếu giờ',
    reset: 'Xóa lọc',
    today: 'Hôm nay',
    thisWeek: 'Tuần này',
    thisMonth: 'Tháng này',
  },

  table: {
    workDate: 'Ngày',
    employee: 'Nhân viên',
    team: 'Tổ',
    checkIn: 'Giờ vào',
    checkOut: 'Giờ ra',
    workedHours: 'Giờ làm',
    workdayValue: 'Công',
    overtime: 'Tăng ca',
    status: 'Tình trạng',
    note: 'Ghi chú',
    attendanceCode: 'Mã chấm công',
    punchCount: 'Số lượt quẹt',
    firstPunch: 'Lượt đầu',
    lastPunch: 'Lượt cuối',
    action: 'Thao tác',
    source: 'Nguồn',
    startedAt: 'Bắt đầu',
    finishedAt: 'Kết thúc',
    received: 'Nhận',
    inserted: 'Thêm mới',
    lastRecordId: 'ID cuối',
    syncStatus: 'Kết quả',
  },

  tag: {
    missingPunch: 'Thiếu giờ ra',
    extraPunch: 'Quẹt nhiều lần',
    sunday: 'Chủ nhật',
    adjusted: 'Chấm hộ',
    normal: 'Đủ công',
  },

  sync: {
    healthyTitle: 'Đồng bộ chấm công bình thường',
    overdueTitle: 'Chưa đồng bộ được chấm công hơn {0} phút', // MSG23
    overdueHint: 'Có thể nhập file dự phòng từ Ronald Jack Pro.',
    never: 'Chưa từng đồng bộ',
    minutesAgo: '{0} phút trước',
    sourceRjSync: 'Máy chấm công',
    sourceExcel: 'File Excel',
    statusRunning: 'Đang chạy',
    statusSucceeded: 'Thành công',
    statusFailed: 'Lỗi',
  },

  map: {
    title: 'Gán mã chấm công',
    description: 'Mã {0} sẽ được gán cho nhân viên, các lượt quẹt cũ của mã này được tính lại.',
    employee: 'Nhân viên',
    employeePlaceholder: 'Chọn nhân viên',
    submit: 'Gán mã',
    cancel: 'Hủy',
    success: 'Đã gán mã chấm công.',
    emptyTitle: 'Không có mã nào chờ gán',
    emptyHint: 'Mọi mã chấm công đã có chủ.',
  },

  import: {
    title: 'Nhập file Excel dự phòng',
    hint: 'Dùng khi không đọc được dữ liệu từ máy chấm công Ronald Jack Pro (UC29).',
    chooseFile: 'Chọn hoặc kéo thả file Excel (.xlsx, .xls)',
    preview: 'Xem trước',
    confirm: 'Nhập dữ liệu',
    confirmTitle: 'Xác nhận nhập file chấm công',
    confirmContent: 'Các lượt quẹt hợp lệ trong file sẽ được ghi vào hệ thống. Bạn có chắc chắn?',
    successPrefix: 'Đã nhập file Excel:',
    previewSummary: {
      rowCount: 'Số dòng đọc được',
      punchCount: 'Số lượt quẹt hợp lệ',
    },
    previewColumns: {
      attendanceCode: 'Mã chấm công',
      workDate: 'Ngày',
      checkIn: 'Giờ vào',
      checkOut: 'Giờ ra',
    },
  },
} as const;

/** Khoảng ngày tối đa một lần xem, khớp TimesheetQueryValidator.MaxRangeDays của BE. */
export const MAX_RANGE_DAYS = 62;

export const DEFAULT_PAGE_SIZE = 20;
