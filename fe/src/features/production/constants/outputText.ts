import type { ShiftCode } from '../types';

export const OUTPUT_TEXT = {
  title: 'Sản lượng cuối ca',
  managerTitle: 'Sửa sản lượng sau hạn',
  subtitle:
    'Chọn ngày, ca và dòng đơn rồi nhập sản lượng cho từng công nhân. Sửa trong hạn: tổ trưởng; sau hạn: Quản đốc (bắt buộc lý do).',
  managerSubtitle:
    'Quản đốc sửa sản lượng của tổ, kể cả sau hạn. Phải ghi lý do, hệ thống lưu nhật ký.',
  date: 'Ngày làm việc',
  shift: 'Ca',
  team: 'Tổ may',
  save: 'Lưu sản lượng',
  reason: 'Lý do sửa (bắt buộc)',
  reasonRequired: 'Quản đốc sửa sản lượng phải ghi lý do.',
  received: 'BTP tổ đã nhận từ kho',
  allocatedHelp:
    'Sản lượng của mỗi công nhân không vượt số lần được giao (màn Giao việc) ở đúng công đoạn đó.',
  noRows: 'Chưa có phân công nào cho dòng đơn này. Tổ trưởng cần phân công công đoạn trước.',
  byWorker: {
    search: 'Tìm mã, tên công nhân',
    stateLabel: 'Tình trạng nhập',
    stateAll: 'Tất cả ({0})',
    stateMissing: 'Còn thiếu ({0})',
    stateDone: 'Đã nhập đủ ({0})',
    noWorkers: 'Không có công nhân phù hợp.',
    columns: { entered: 'Công đoạn đã nhập', total: 'Tổng sản lượng' },
    enteredValue: '{0} / {1}',
    enter: 'Nhập sản lượng',
    modalTitle: 'Nhập sản lượng: {0}',
    modalHint:
      'Các công đoạn lấy từ màn Giao việc (công nhân đã xác nhận). Nhập ở dòng nhóm để điền cùng số cho các công đoạn nhập được trong nhóm, rồi sửa lại từng công đoạn nếu cần. Số 0 ở dòng đã nhập nghĩa là xóa dòng đó.',
    groupRow: 'Nhóm {0} (nhập được {1}/{2} công đoạn)',
    noGroup: 'chưa đặt tên',
    groupInput: 'Sản lượng nhóm {0}',
    groupPlaceholder: 'Cả nhóm',
    groupChanged: 'Đã sửa {0}',
    searchOperation: 'Tìm mã, tên công đoạn',
    noMatchOperation: 'Không có công đoạn khớp.',
    operationInput: 'Sản lượng {0}',
    noOperations:
      'Công nhân chưa được giao việc nào ở dòng đơn này. Hãy giao ở màn Giao việc và chờ công nhân xác nhận.',
    overTitle: 'Chưa lưu được vì có số vượt số lần được giao:',
    overCap: '{0}: chỉ còn được báo {1}.',
    overCapTransfer:
      '{0}: chỉ còn được báo {1}, người cùng công đoạn và phần tổ chưa giao chỉ chuyển được thêm {2}.',
    transferTitle: 'Có công đoạn báo vượt số được giao, khi lưu sẽ chuyển phần vượt:',
    transferLine: '{0}: vượt {1}, lấy từ người cùng công đoạn còn dư rồi phần tổ chưa giao.',
    transferConfirmTitle: 'Chuyển phần vượt rồi lưu sản lượng?',
    transferConfirmContent:
      'Hệ thống ghi phiếu thu hồi (hạ số giao) ở người cùng công đoạn còn dư, thiếu thì lấy phần tổ chưa giao, rồi giao phần vượt cho công nhân này trước khi lưu sản lượng.',
    overNotAllocated:
      '{0}: công nhân chưa được giao việc này. Hãy giao ở màn Giao việc và chờ xác nhận.',
  },
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
} as const;

export const QC_TEXT = {
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
} as const;
