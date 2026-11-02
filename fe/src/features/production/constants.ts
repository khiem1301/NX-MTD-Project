import type { StatusLabel } from '@/components/common/StatusTag';
import type {
  DefectObjectionFilter,
  DefectReason,
  DefectStatus,
  DefectType,
  PieceRateStatus,
  ShiftCode,
} from './types';

/** Số lượng hàng hỏng tối đa mỗi phiếu và độ dài ghi chú / lý do / trả lời, khớp DefectLimits ở BE. */
export const DEFECT_MAX_QTY = 1_000_000;
export const DEFECT_TEXT_MAX = 500;
/** Khoảng ngày lọc danh sách phiếu hàng hỏng tối đa (ngày), khớp BE. */
export const DEFECT_MAX_RANGE_DAYS = 366;

/** Số lượng sản lượng tối đa mỗi dòng, khớp OutputLimits.MaxQty ở BE. */
export const OUTPUT_MAX_QTY = 1_000_000;
/** Khớp AssignmentLimits.MaxOperations ở BE. */
export const ASSIGNMENT_MAX_OPERATIONS = 500;
/** Khớp `AssignmentLimits.MaxOrderLines` ở BE. */
export const ALLOT_MAX_LINES = 500;
/** Khớp `OperationLimits` ở BE. */
export const OPERATION_LIMITS = {
  maxSequence: 32767,
  minSkill: 1,
  maxSkill: 9,
  maxStandardTimeSec: 9999999.99,
  maxGroupName: 200,
  maxGroupChange: 500,
  maxSearch: 100,
  maxCode: 30,
  maxName: 200,
  codePattern: /^[A-Za-z0-9._-]+$/,
} as const;
/** Khớp `PieceRateLimits` ở BE. */
export const PIECE_RATE_LIMITS = { min: 0.01, max: 100_000_000 } as const;
export const ASSIGNMENT_SEARCH_MAX_LENGTH = 100;

/** Chuỗi hiển thị của luồng công đoạn, đơn giá, phân công, sản lượng, QC, hàng hỏng (KI-01..KI-11). Lỗi nghiệp vụ lấy message từ BE. */
export const SEWING_TEAM_TYPE = 'SEWING';

export const PRODUCTION_TEXT = {
  saved: 'Đã lưu.',
  required: 'Vui lòng nhập thông tin này.',
  selectProduct: 'Chọn mã hàng',
  selectOrder: 'Chọn đơn hàng',
  selectOrderLine: 'Chọn dòng đơn (màu - size)',
  selectTeam: 'Chọn tổ may',
  selectWorker: 'Chọn công nhân',
  selectOperation: 'Chọn công đoạn',
  selectFirst: 'Chọn đơn hàng trước.',

  operations: {
    title: 'Công đoạn và đơn giá',
    subtitle:
      'Nhập công đoạn của mã hàng từ Excel rồi đặt đơn giá từng công đoạn (có ngày hiệu lực), gửi Admin duyệt. Mỗi mã hàng có đúng một công đoạn cuối đang dùng.',
    adminSubtitle:
      'Nhập và sửa công đoạn, nhóm công đoạn của mã hàng. Đơn giá do Kế toán đặt, Admin duyệt ở màn Duyệt đơn giá công đoạn. Mỗi mã hàng có đúng một công đoạn cuối đang dùng.',
    summary: '{0} công đoạn đang dùng, {1} chưa có nhóm, {2} chưa có đơn giá được duyệt.',
    noRate: 'Chưa có',
    selectProductHint:
      'Chọn mã hàng để xem, nhập công đoạn và đơn giá. Mã hàng chưa có công đoạn thì bấm Nhập từ Excel hoặc Thêm công đoạn.',
    rateFilter: {
      label: 'Đơn giá',
      missing: 'Chưa có giá duyệt ({0})',
      draft: 'Có bản Nháp ({0})',
      pending: 'Đang chờ duyệt ({0})',
      rejected: 'Bị từ chối ({0})',
    },
    finalDiscontinueConfirm:
      'Đây là công đoạn cuối. Ngừng dùng thì mã hàng không còn công đoạn cuối: chưa tính được hàng hoàn thiện và chưa kết thúc được chỉ tiêu KPI cho đến khi chọn công đoạn cuối khác.',
    manageGroups: 'Nhóm công đoạn',
    noGroupFilter: 'Chưa có nhóm ({0})',
    groups: {
      title: 'Nhóm công đoạn của {0}',
      hint: 'Nhóm chỉ để gom và tìm công đoạn, không ảnh hưởng phân công, đơn giá, sản lượng.',
      addPlaceholder: 'Tên nhóm mới, vd May tay',
      add: 'Thêm nhóm',
      name: 'Tên nhóm',
      count: 'Số công đoạn',
      rename: 'Đổi tên',
      remove: 'Xóa nhóm',
      removeConfirm: 'Xóa nhóm này? {0} công đoạn trong nhóm sẽ về chưa có nhóm.',
      empty: 'Mã hàng này chưa có nhóm công đoạn.',
    },
    bulk: {
      selected: 'Đã chọn {0} công đoạn. Xếp vào:',
      group: 'Chọn nhóm',
      noGroups: 'Chưa có nhóm. Thêm ở nút Nhóm công đoạn.',
      apply: 'Xếp nhóm',
      ungroup: 'Bỏ nhóm',
      clear: 'Bỏ chọn',
      tooMany: 'Mỗi lần đổi tối đa {0} công đoạn, hãy bỏ bớt.',
      done: 'Đã đổi nhóm {0} công đoạn.',
      none: 'Các công đoạn đã chọn đã đúng nhóm này, không có gì thay đổi.',
      submitRates: 'Gửi duyệt {0} đơn giá Nháp',
      submitConfirm: 'Gửi {0} đơn giá Nháp cho Admin duyệt?',
      submitDone: 'Đã gửi duyệt {0} đơn giá.',
    },
    edit: 'Sửa công đoạn',
    add: 'Thêm công đoạn',
    addTitle: 'Thêm công đoạn',
    addHint: 'Thêm một công đoạn cho mã hàng đang chọn, rồi nhập đơn giá và gửi Admin duyệt.',
    importExcel: 'Nhập từ Excel',
    noFinalStep:
      'Mã hàng này chưa có công đoạn cuối. Bấm "Sửa" ở công đoạn cuối của quy trình và tick "Là công đoạn cuối".',
    import: {
      title: 'Nhập công đoạn từ file Excel quy trình công nghệ',
      hint: 'File có bảng với các cột STT, BƯỚC CÔNG VIỆC, BẬC, TGCT. Mã công đoạn tự sinh từ STT (STT 7 thành OP07), nhập lại file sẽ cập nhật đúng công đoạn cũ, không tạo trùng.',
      chooseFile: 'Bấm hoặc kéo file .xlsx vào đây',
      chooseFileHint:
        'Chỉ đọc cột STT, BƯỚC CÔNG VIỆC, BẬC, TGCT, THIẾT BỊ. Dòng tiêu đề nhóm, chỗ trống và "Dự phòng" được bỏ qua.',
      productInFile: 'Mã hàng ghi trong file',
      productCheck: 'Đối chiếu với mã hàng bạn đang chọn trước khi nhập.',
      sheet: 'Sheet',
      finalStep: 'Công đoạn cuối (không bắt buộc)',
      finalStepPlaceholder: 'Chọn công đoạn cuối',
      finalStepHasOne: 'Mã hàng đã có công đoạn cuối, không chọn thêm.',
      preview: 'Xem trước',
      confirm: 'Nhập công đoạn',
      confirmTitle: 'Nhập công đoạn vào mã hàng?',
      confirmContent:
        'Công đoạn mới được thêm, công đoạn trùng mã được cập nhật. Không công đoạn nào bị xóa.',
      summary: { created: 'Thêm mới', updated: 'Cập nhật', unchanged: 'Không đổi', error: 'Lỗi' },
      resultTemplate:
        'Đã nhập công đoạn: thêm {0}, cập nhật {1}, không đổi {2}, bỏ qua {3} dòng lỗi.',
      rateResultTemplate: ' Đơn giá: thêm {0}, cập nhật {1}, không đổi {2}, bỏ qua {3}.',
      withRates: 'Nhập kèm đơn giá từ cột TIỀN',
      noRateColumn: 'File không có cột TIỀN nên không nhập được đơn giá.',
      effectiveDate: 'Đơn giá hiệu lực từ',
      submitRates: 'Gửi Admin duyệt luôn (bỏ chọn thì lưu Nháp)',
      rateHint:
        'Đơn giá cùng ngày đã chờ duyệt hoặc đã duyệt với giá khác sẽ được bỏ qua; đơn giá bằng giá hiện hành không tạo thêm.',
      rateSummary: {
        created: 'Đơn giá mới',
        updated: 'Đơn giá cập nhật',
        unchanged: 'Đơn giá không đổi',
        skipped: 'Đơn giá bỏ qua',
      },
      columns: {
        stt: 'STT',
        code: 'Mã',
        name: 'Bước công việc',
        skill: 'Bậc',
        time: 'TG chuẩn (giây)',
        equipment: 'Thiết bị',
        rate: 'Đơn giá (đ)',
        action: 'Kết quả',
      },
      rateAction: {
        New: { text: 'Đơn giá mới', color: 'green' },
        Update: { text: 'Cập nhật giá', color: 'blue' },
        Unchanged: { text: 'Giá không đổi', color: 'default' },
        Skipped: { text: 'Bỏ qua giá', color: 'orange' },
      } as Record<string, StatusLabel>,
      action: {
        New: { text: 'Thêm mới', color: 'green' },
        Update: { text: 'Cập nhật', color: 'blue' },
        Unchanged: { text: 'Không đổi', color: 'default' },
        Error: { text: 'Lỗi', color: 'red' },
      } as Record<string, StatusLabel>,
    },
    discontinue: 'Ngừng dùng',
    reactivate: 'Dùng lại',
    discontinueConfirm: 'Ngừng dùng công đoạn này? Sản lượng cũ vẫn giữ nguyên.',
    columns: {
      code: 'Mã',
      name: 'Tên công đoạn',
      sequence: 'Thứ tự',
      skill: 'Bậc',
      standardTime: 'TG chuẩn (s)',
      teamType: 'Loại tổ',
      operation: 'Công đoạn',
      isFinal: 'Cuối',
      group: 'Nhóm công đoạn',
      rate: 'Đơn giá',
      status: 'Trạng thái',
    },
    /** Dòng đơn giá mới dưới đơn giá đang áp dụng: "→ 1.700 ₫ từ 22/11/2026". */
    nextRateLine: '→ {0} từ {1}',
    form: {
      code: 'Mã công đoạn',
      name: 'Tên công đoạn',
      sequence: 'Thứ tự',
      skill: 'Bậc (1-9)',
      standardTime: 'TG chuẩn (giây)',
      isFinal: 'Là công đoạn cuối (tính hàng hoàn thiện)',
      isFinalMoves: 'Công đoạn cuối hiện tại sẽ chuyển sang công đoạn này.',
      codeFormat: 'Mã chỉ gồm chữ không dấu, số và các ký tự . _ -',
      group: 'Nhóm công đoạn',
      groupHint:
        'Chọn hoặc gõ tên mới (vd May tay) để nhóm các công đoạn nhỏ; tìm theo tên này ở màn Giao việc.',
      components: 'Chi tiết công đoạn làm trên',
      componentsHint:
        'Một công đoạn có thể làm trên nhiều chi tiết (vd Tra tay: Tay áo, Thân trước, Thân sau). Bỏ trống nếu không gắn chi tiết.',
    },
    filterGroup: 'Nhóm',
    searchOperation: 'Tìm mã, tên',
    teamType: { Sewing: 'Tổ may', Cutting: 'Tổ cắt' } as Record<string, string>,
    status: {
      Active: { text: 'Đang dùng', color: 'green' },
      Discontinued: { text: 'Đã ngừng', color: 'default' },
    } as Record<string, StatusLabel>,
  },

  pieceRates: {
    adminTitle: 'Duyệt đơn giá công đoạn',
    adminSubtitle: 'Đơn giá chờ duyệt. Sản lượng chỉ nhập được khi công đoạn có đơn giá đã duyệt.',
    add: 'Đặt đơn giá',
    history: 'Lịch sử đơn giá',
    noHistory: 'Công đoạn này chưa có đơn giá nào.',
    setRate: 'Đặt đơn giá',
    editRate: 'Sửa đơn giá',
    edit: 'Sửa đơn giá',
    submit: 'Gửi duyệt',
    submitConfirm: 'Gửi đơn giá này cho Admin duyệt?',
    approve: 'Duyệt',
    approveTitle: 'Duyệt đơn giá',
    reject: 'Từ chối',
    rejectTitle: 'Từ chối đơn giá',
    approveSelected: 'Duyệt các dòng đã chọn',
    approveSelectedTitle: 'Duyệt các đơn giá đã chọn',
    selectAllPending: 'Chọn tất cả đơn giá chờ duyệt trong trang',
    note: 'Ghi chú (không bắt buộc)',
    allStatus: 'Mọi trạng thái',
    allTab: 'Tất cả',
    columns: {
      product: 'Mã hàng',
      operation: 'Công đoạn',
      rate: 'Đơn giá',
      effectiveDate: 'Hiệu lực từ',
      status: 'Trạng thái',
      note: 'Ghi chú / lý do từ chối',
    },
    form: {
      operation: 'Công đoạn',
      rate: 'Đơn giá (đồng/sản phẩm)',
      effectiveDate: 'Ngày hiệu lực',
      rateMin: 'Đơn giá phải lớn hơn 0.',
      rateMax: 'Đơn giá không được vượt quá 100.000.000 đồng.',
      pastDateHint:
        'Ngày hiệu lực đã qua: sau khi duyệt, đơn giá này áp dụng cho sản lượng nhập mới của các ngày từ đó. Sản lượng đã nhập giữ nguyên đơn giá cũ.',
      currentRate: 'Đơn giá hiện hành',
    },
    done: {
      submitted: 'Đã gửi duyệt.',
      approved: 'Đã duyệt đơn giá.',
      rejected: 'Đã từ chối đơn giá.',
    },
    status: {
      Draft: { text: 'Nháp', color: 'default' },
      PendingApproval: { text: 'Chờ duyệt', color: 'orange' },
      Approved: { text: 'Đã duyệt', color: 'green' },
      Rejected: { text: 'Bị từ chối', color: 'red' },
    } as Record<PieceRateStatus, StatusLabel>,
  },

  orderLineTeams: {
    title: 'Chia đơn cho tổ may',
    subtitle:
      'Chia từng màu, size của đơn cho tổ may làm. Tổ chỉ xin và nhận BTP của màu, size được chia, và phân công công đoạn của mã hàng có màu, size đó. Một dòng có thể chia cho nhiều tổ.',
    search: 'Tìm đơn, mã hàng, màu, size',
    onlyUnallotted: 'Chỉ dòng chưa chia',
    teamPlaceholder: 'Chưa chia, chọn tổ may',
    teamAria: 'Tổ may của {0} - {1} - {2}/{3}',
    teamGone: '{0} (ngừng hoạt động)',
    saved: 'Đã lưu việc chia đơn.',
    empty: 'Không có dòng đơn nào trong các đơn đang mở.',
    selectedCount: 'Đã chọn {0} dòng. Chia thêm cho:',
    bulkTeams: 'Chọn tổ may',
    bulkApply: 'Chia',
    bulkClear: 'Bỏ chọn',
    tooManySelected: 'Mỗi lần chia tối đa {0} dòng, hãy bỏ bớt.',
    bulkDone: 'Đã thêm {0} lượt chia.',
    bulkNone: 'Các dòng đã chọn đều đã chia cho tổ này, không có gì thay đổi.',
    columns: {
      order: 'Đơn hàng',
      dueDate: 'Ngày giao',
      product: 'Mã hàng',
      color: 'Màu',
      size: 'Size',
      qty: 'Số lượng',
      teams: 'Tổ may',
    },
  },

  assignments: {
    title: 'Phân công công đoạn',
    subtitle:
      'Đặt công nhân cho từng công đoạn của đơn, làm cơ sở cho cả đợt làm đơn. Phải có đủ người ở mọi công đoạn trước khi nhận BTP. Đổi người giữ lại lịch sử.',
    workerPlaceholder: 'Chưa có người, chọn công nhân',
    search: 'Tìm mã, tên',
    filterGroup: 'Nhóm',
    filterWorker: 'Công nhân',
    filterState: 'Tình trạng',
    workerLoad: '{0} ({1} công đoạn)',
    stateAll: 'Tất cả ({0})',
    stateMissing: 'Chưa có người ({0})',
    stateCovered: 'Đã có người ({0})',
    selectedCount: 'Đã chọn {0} công đoạn. Giao cho:',
    bulkWorker: 'Chọn công nhân',
    bulkApply: 'Giao việc',
    bulkClear: 'Bỏ chọn',
    selectOrderHint:
      'Chọn đơn hàng để đặt công nhân cho từng công đoạn. Chỉ hiện đơn có mã hàng được chia cho tổ.',
    noOrders: 'Chưa có đơn nào được chia cho tổ. Quản đốc cần chia mã hàng cho tổ trước.',
    selectProduct: 'Mã hàng',
    productOption: '{0} ({1}/{2} công đoạn)',
    productNoOperations: '{0} (chưa khai công đoạn)',
    noOperations:
      'Mã hàng {0} chưa có công đoạn may đang dùng nên tổ chưa phân công và chưa nhận được BTP của mã này. Kế toán cần khai công đoạn trước.',
    noMatch: 'Không có công đoạn phù hợp với bộ lọc.',
    views: { operation: 'Theo công đoạn', worker: 'Theo công nhân' },
    byWorker: {
      searchWorker: 'Tìm mã, tên công nhân',
      workerAll: 'Tất cả ({0})',
      workerIdle: 'Chưa có việc ({0})',
      noWorkerMatch: 'Không có công nhân phù hợp.',
      columns: { worker: 'Công nhân', load: 'Số công đoạn', operations: 'Công đoạn đang làm' },
      modalColumns: {
        group: 'Nhóm',
        skill: 'Bậc',
        time: 'Thời gian (giây)',
        workers: 'Số người làm',
      },
      noOperationsYet: 'Chưa có việc',
      moreOperations: '+{0}',
      pick: 'Chọn công đoạn',
      modalTitle: 'Chọn công đoạn cho {0}',
      modalHint: 'Tích các công đoạn công nhân này làm, rồi bấm Lưu. Bỏ tích là thôi giao.',
      changes: 'Thêm {0}, bỏ {1}',
      noChanges: 'Chưa thay đổi gì.',
      saved: 'Đã lưu phân công.',
      tooManyAdded: 'Mỗi lần lưu thêm tối đa {0} công đoạn, hãy bỏ bớt.',
    },
    noWorkers: 'Tổ chưa có công nhân đang làm.',
    tooManySelected: 'Mỗi lần giao tối đa {0} công đoạn, hãy bỏ bớt.',
    copySameOrder: 'Đơn nguồn phải khác đơn đang chọn.',
    workerGone: '{0} (đã nghỉ hoặc chuyển tổ)',
    coverageDone: 'Đã phân công đủ {0} công đoạn. Tổ có thể nhận BTP của mã hàng này.',
    coverageMissing:
      'Còn {0}/{1} công đoạn chưa có người. Phân công đủ thì tổ mới nhận được BTP của mã hàng này.',
    copy: 'Chép từ đơn khác',
    copyTitle: 'Chép phân công từ đơn khác',
    copyFrom: 'Đơn nguồn',
    copyHint:
      'Chép phân công đang hiệu lực của tổ từ đơn nguồn sang đơn đang chọn (theo mã công đoạn).',
    columns: {
      operation: 'Công đoạn',
      skill: 'Bậc',
      worker: 'Công nhân',
    },
    done: {
      assigned: 'Đã phân công.',
      closed: 'Đã bỏ phân công.',
      added: 'Đã giao {0} công đoạn.',
      addedSkipped: 'Đã giao {0} công đoạn, bỏ qua {1} công đoạn công nhân đang làm.',
      noneAdded: 'Công nhân đã làm tất cả công đoạn đã chọn, không có gì thay đổi.',
      copied: 'Đã chép {0} phân công.',
      noneCopied: 'Không có phân công nào để chép từ đơn này.',
    },
    empty: 'Mã hàng này chưa có công đoạn may đang dùng.',
  },

  output: {
    title: 'Sản lượng cuối ca',
    managerTitle: 'Sửa sản lượng sau hạn',
    subtitle:
      'Nhập số sản phẩm của từng công nhân theo công đoạn trong ca. Sửa trong hạn: tổ trưởng; sau hạn: Quản đốc (bắt buộc lý do).',
    managerSubtitle:
      'Quản đốc sửa sản lượng của tổ, kể cả sau hạn. Phải ghi lý do, hệ thống lưu nhật ký.',
    date: 'Ngày làm việc',
    shift: 'Ca',
    team: 'Tổ may',
    save: 'Lưu sản lượng',
    reason: 'Lý do sửa (bắt buộc khi sửa sau hạn)',
    reasonRequired: 'Vui lòng nhập lý do khi sửa sau hạn.',
    received: 'BTP tổ đã nhận từ kho',
    allocatedHelp:
      'Sản lượng của mỗi công nhân không vượt số lần được giao (màn Giao việc) ở đúng công đoạn đó.',
    noRows:
      'Chưa có phân công nào cho dòng đơn này. Hãy phân công công đoạn trước, hoặc thêm dòng ngoài phân công bên trên.',
    addOutside: 'Thêm dòng ngoài phân công',
    addRow: 'Thêm dòng',
    removeRow: 'Bỏ dòng',
    nothingChanged: 'Chưa có thay đổi nào để lưu.',
    columns: {
      operation: 'Công đoạn',
      worker: 'Công nhân',
      qty: 'Số lượng',
      allocated: 'Đã báo / được giao',
      rate: 'Đơn giá khóa',
      deadline: 'Hạn sửa',
      warnings: 'Cảnh báo',
    },
    tags: {
      outside: 'Ngoài phân công',
      noAttendance: 'Chưa chấm công',
      noRate: 'Chưa có đơn giá duyệt',
      notAllocated: 'Chưa được giao việc',
      locked: 'Hết hạn sửa',
    },
    resultTemplate: 'Đã lưu: thêm {0}, sửa {1}, xóa {2}.',
    shiftLabel: { Morning: 'Ca sáng', Afternoon: 'Ca chiều', Overtime: 'Tăng ca' } as Record<
      ShiftCode,
      string
    >,
  },

  qc: {
    title: 'Hàng hoàn thiện (QC)',
    managerTitle: 'Sửa số hàng đạt QC',
    subtitle:
      'QA/QC nhập số hàng đạt theo đợt kiểm. Số đạt không vượt hàng đang có trên chuyền. Sau hết ca chỉ Quản đốc sửa.',
    managerSubtitle: 'Quản đốc sửa hoặc xóa đợt QC, kể cả sau hết ca. Phải ghi lý do.',
    add: 'Thêm đợt kiểm',
    addTitle: 'Thêm đợt kiểm hoàn thiện',
    editTitle: 'Sửa số hàng đạt',
    deleteTitle: 'Xóa đợt kiểm',
    edit: 'Sửa',
    delete: 'Xóa',
    onLine: 'Hàng trên chuyền',
    onLineDetail: 'Nhận {0} - đạt {1} - hỏng hủy {2} = còn {3}',
    filterDate: 'Lọc theo ngày',
    columns: {
      date: 'Ngày',
      shift: 'Ca',
      time: 'Giờ',
      team: 'Tổ',
      order: 'Đơn hàng',
      line: 'Mã hàng - màu - size',
      qty: 'Số đạt',
      inspector: 'Người kiểm',
      deadline: 'Hạn sửa',
    },
    form: {
      date: 'Ngày',
      shift: 'Ca',
      time: 'Giờ kiểm',
      team: 'Tổ may',
      line: 'Dòng đơn',
      qty: 'Số hàng đạt',
      qtyMin: 'Số hàng đạt phải lớn hơn 0.',
      qtyMax: 'Số hàng đạt tối đa 1.000.000.',
      reason: 'Lý do (bắt buộc khi sửa sau hết ca)',
    },
    done: {
      created: 'Đã thêm đợt kiểm.',
      updated: 'Đã cập nhật đợt kiểm.',
      deleted: 'Đã xóa đợt kiểm.',
    },
  },

  defects: {
    title: 'Hàng hỏng',
    managerTitle: 'Hàng hỏng của các tổ',
    subtitle:
      'Ghi hàng hỏng (sửa lại hoặc hủy) và công nhân gây lỗi. Công nhân được thông báo và có quyền phản hồi nếu ghi sai.',
    managerSubtitle:
      'Quản đốc xem phiếu hàng hỏng của các tổ. Công nhân phản đối thì chấp nhận phiếu (giữ phiếu) hoặc hủy phiếu ghi sai (bắt buộc lý do).',
    add: 'Ghi hàng hỏng',
    addTitle: 'Ghi hàng hỏng',
    cancel: 'Hủy phiếu',
    cancelTitle: 'Hủy phiếu hàng hỏng',
    filterDate: 'Lọc theo ngày',
    searchPlaceholder: 'Tìm theo công nhân, mã hàng, tổ, mã phiếu',
    objected: 'Công nhân phản hồi',
    confirm: 'Chấp nhận phiếu',
    confirmTitle: 'Chấp nhận phiếu hàng hỏng',
    confirmHint:
      'Công nhân phản đối phiếu này. Chấp nhận thì phiếu được giữ nguyên và công nhân không phản đối lại được; nếu phiếu ghi sai hãy hủy phiếu.',
    confirmNote: 'Ghi chú (không bắt buộc)',
    confirmWorker: 'Phản đối của',
    rangeTooLong: 'Khoảng ngày lọc tối đa 366 ngày.',
    filters: {
      range: ['Từ ngày', 'Đến ngày'] as [string, string],
      team: 'Tổ',
      type: 'Loại hỏng',
      status: 'Phiếu',
      objection: 'Phản đối',
    },
    objectionOptions: [
      { value: 'None', label: 'Không phản đối' },
      { value: 'Pending', label: 'Chờ quyết định' },
      { value: 'Resolved', label: 'Đã quyết định' },
    ] as { value: DefectObjectionFilter; label: string }[],
    state: {
      Pending: { text: 'Đã ghi', color: 'blue' },
      ObjectionPending: { text: 'Chờ quyết định', color: 'volcano' },
      Confirmed: { text: 'Đã chấp nhận', color: 'green' },
      Cancelled: { text: 'Đã hủy', color: 'default' },
    } as Record<string, StatusLabel>,
    detail: {
      reasonNote: 'Ghi chú lý do',
      rate: 'Đơn giá trừ',
      deadline: 'Hạn sửa',
      recordedAt: 'Ghi lúc',
      updatedAt: 'Cập nhật lần cuối',
      objection: 'Phản đối của công nhân',
      replacement: 'Đơn cắt bù',
    },
    columns: {
      ticket: 'Phiếu',
      orderProduct: 'Đơn / mã hàng',
      operationWorker: 'Công đoạn / công nhân',
      defect: 'Hỏng',
      state: 'Tình trạng',
      code: 'Mã phiếu',
      date: 'Ngày',
      team: 'Tổ',
      order: 'Đơn hàng',
      operation: 'Công đoạn',
      worker: 'Công nhân',
      type: 'Loại',
      qty: 'Số lượng',
      reason: 'Lý do',
      rate: 'Đơn giá trừ',
      status: 'Trạng thái',
      objection: 'Phản hồi của công nhân',
      deadline: 'Hạn sửa',
    },
    form: {
      date: 'Ngày',
      shift: 'Ca',
      line: 'Dòng đơn',
      operation: 'Công đoạn',
      worker: 'Công nhân gây lỗi',
      type: 'Loại hỏng',
      qty: 'Số lượng',
      qtyMin: 'Số lượng phải lớn hơn 0.',
      qtyMax: 'Số lượng tối đa 1.000.000.',
      reason: 'Lý do',
      note: 'Ghi chú',
      noteRequired: 'Lý do "Khác" bắt buộc ghi chú.',
    },
    done: {
      created: 'Đã ghi hàng hỏng và thông báo cho công nhân.',
      cancelled: 'Đã hủy phiếu hàng hỏng.',
      confirmed: 'Đã chấp nhận phiếu hàng hỏng.',
    },
    type: {
      Rework: { text: 'Sửa lại', color: 'gold' },
      Scrap: { text: 'Hủy (trừ lương)', color: 'red' },
    } as Record<DefectType, StatusLabel>,
    reason: {
      SkippedStitch: 'Bỏ mũi',
      CrookedSeam: 'Đường may lệch',
      WrongSize: 'Sai size',
      Torn: 'Rách',
      Stained: 'Dơ bẩn',
      Other: 'Khác',
    } as Record<DefectReason, string>,
    status: {
      Recorded: { text: 'Đã ghi', color: 'blue' },
      Confirmed: { text: 'Đã chấp nhận', color: 'green' },
      Cancelled: { text: 'Đã hủy', color: 'default' },
    } as Record<DefectStatus, StatusLabel>,
  },

  replacement: {
    button: 'Đơn cắt bù',
    title: 'Đơn cắt bù từ phiếu hàng hỏng',
    hint: 'Gửi Quản đốc duyệt để cắt bù các chi tiết của số hàng đã hủy. Mỗi phiếu hàng hỏng chỉ có một đơn cắt bù.',
    defect: 'Phiếu hàng hỏng',
    panels: 'Chi tiết cần cắt bù',
    panelName: 'Chọn chi tiết (thân trước, tay, cổ...)',
    quantity: 'Số lượng',
    addPanel: 'Thêm chi tiết',
    targetDate: 'Ngày cần',
    content: 'Ghi chú',
    panelRequired: 'Chọn chi tiết cần cắt bù.',
    qtyRange: 'Số lượng từ 1 đến số hàng đã hủy.',
    submit: 'Gửi đơn',
    done: 'Đã gửi đơn cắt bù cho Quản đốc duyệt.',
    column: 'Đơn cắt bù',
  },

  myDefects: {
    title: 'Hàng hỏng của tôi',
    subtitle:
      'Các phiếu hàng hỏng tổ trưởng ghi cho bạn. Hàng hủy trừ vào lương tạm tính; nếu ghi sai hãy phản hồi.',
    object: 'Phản hồi',
    objectTitle: 'Phản hồi phiếu hàng hỏng',
    objection: 'Nội dung phản hồi',
    objectionRequired: 'Vui lòng nhập nội dung phản hồi.',
    done: 'Đã gửi phản hồi cho tổ trưởng.',
    empty: 'Bạn chưa có phiếu hàng hỏng nào.',
    rejected: 'Phản hồi bị bác',
  },
} as const;

/** BE trả ca dạng "MORNING" hoặc "Morning": quy về nhãn tiếng Việt. */
export const shiftText = (shift: string | null | undefined): string => {
  if (!shift) return '—';
  const key = (shift.charAt(0).toUpperCase() + shift.slice(1).toLowerCase()) as ShiftCode;
  return PRODUCTION_TEXT.output.shiftLabel[key] ?? shift;
};

export const SHIFT_OPTIONS = (Object.keys(PRODUCTION_TEXT.output.shiftLabel) as ShiftCode[]).map(
  (value) => ({
    value,
    label: PRODUCTION_TEXT.output.shiftLabel[value],
  }),
);

/** Lý do tổ trưởng được chọn khi ghi phiếu hàng hỏng. */
export const DEFECT_REASON_OPTIONS = (
  Object.keys(PRODUCTION_TEXT.defects.reason) as DefectReason[]
).map((value) => ({
  value,
  label: PRODUCTION_TEXT.defects.reason[value],
}));
