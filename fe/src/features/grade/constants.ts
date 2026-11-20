import type { StatusLabel } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';

export const EVAL_STATUS = {
  SUBMITTED: 'SUBMITTED',
  APPLIED: 'APPLIED',
  NOT_APPLIED: 'NOT_APPLIED',
} as const;

export const EVAL_STATUS_LABEL: Record<string, StatusLabel> = {
  SUBMITTED: { text: 'Chờ quyết định', color: 'gold' },
  APPLIED: { text: 'Đã áp dụng', color: 'green' },
  NOT_APPLIED: { text: 'Không áp dụng', color: 'default' },
};

export const GR_LABELS = {
  title: 'Bậc thợ',
  evaluationsTab: 'Đề xuất bậc',
  levelsTab: 'Bậc & mức tiền',
  addCode: 'Thêm bậc',
  searchGrade: 'Tìm theo mã, tên bậc...',
  codeHint:
    'Mỗi dòng là một bậc và mức tiền đang áp dụng. Đổi tiền = thêm mức mới theo ngày hiệu lực, mức cũ giữ làm lịch sử. Bậc đã có người, đề xuất hoặc tiền bậc dùng thì không xóa được, chỉ ngừng dùng.',
  codeCode: 'Mã bậc (chữ hoa / số, tối đa 5 ký tự)',
  active: 'Đang dùng',
  inactive: 'Ngừng dùng',
  inUse: 'Đã được dùng',
  deactivate: 'Ngừng dùng',
  reactivate: 'Dùng lại',
  deleteConfirm: 'Xóa bậc này?',
  deleteLevelConfirm: 'Xóa mức tiền bậc chưa hiệu lực này?',
  editLevel: 'Sửa mức tiền bậc',
  futureOnlyHint: 'Mức đã có hiệu lực là lịch sử lương, không sửa hoặc xóa.',
  propose: 'Đề xuất bậc',
  setDirect: 'Đặt bậc trực tiếp',
  setDirectHint:
    'Đặt bậc ngay, không cần tổ trưởng đề xuất. Đề xuất đang chờ của công nhân này (nếu có) sẽ bị đóng lại.',
  newGrade: 'Bậc mới',
  setReason: 'Lý do',
  worker: 'Công nhân',
  selectWorker: 'Chọn công nhân',
  selectTeam: 'Chọn tổ trước',
  selectTeamFirst: 'Chọn tổ để xem công nhân',
  noTeam: 'Chưa có tổ',
  currentGrade: 'Bậc hiện tại',
  proposedGrade: 'Bậc đề xuất',
  reason: 'Lý do đề xuất',
  defects: 'Số lỗi 30 ngày',
  team: 'Tổ',
  proposedBy: 'Người đề xuất',
  date: 'Ngày đề xuất',
  status: 'Trạng thái',
  decisionNote: 'Ghi chú quyết định',
  effectiveDate: 'Hiệu lực từ',
  apply: 'Áp dụng',
  notApply: 'Không áp dụng',
  notApplyTitle: 'Không áp dụng đề xuất bậc',
  applyTitle: 'Áp dụng bậc thợ',
  pending: 'Đang chờ đề xuất',
  addLevel: 'Đặt mức tiền bậc',
  changeAmount: 'Đổi tiền',
  currentAmount: 'Mức hiện tại',
  newAmount: 'Mức mới / tháng',
  levelHistory: 'Lịch sử mức tiền và mức sắp áp dụng',
  upcoming: 'Sắp áp dụng',
  expired: 'Đã thay thế',
  diffFromPrev: 'Chênh so với mức trước',
  sameAmount: 'Mức mới đang trùng mức hiện tại.',
  effectiveHint:
    'Mức cũ được giữ làm lịch sử. Chọn ngày sau ngày hiệu lực của mức hiện tại, kể cả ngày trong tương lai.',
  replaceHint: 'Ngày này đã có mức hẹn, lưu sẽ thay thế mức đó',
  allGrades: 'Mọi bậc',
  gradeCode: 'Bậc',
  amount: 'Tiền bậc / tháng',
  current: 'Đang áp dụng',
  none: 'Chưa có',
} as const;

export const GR_MESSAGES = {
  proposed: 'Đã gửi đề xuất bậc thợ.',
  set: 'Đã đặt bậc thợ.',
  applied: 'Đã áp dụng bậc thợ.',
  notApplied: 'Đã ghi nhận không áp dụng bậc thợ.',
  levelSaved: 'Đã lưu mức tiền bậc.',
  levelDeleted: 'Đã xóa mức tiền bậc.',
  codeSaved: 'Đã lưu bậc thợ.',
  codeDeleted: 'Đã xóa bậc thợ.',
  levelHint: 'Đổi tiền bậc = thêm một dòng mới với ngày hiệu lực mới, các dòng cũ giữ làm lịch sử.',
} as const;

/** Luật bắt buộc nhập dùng chung cho các form của màn Bậc thợ. */
export const REQUIRED_RULES = [{ required: true, message: MESSAGES.common.required }];

// ---------- AI gợi ý bậc cho cả tổ (DEV-ALL-148) ----------

/** Bộ lọc dòng trong modal AI gợi ý. */
export const AI_FILTER = {
  CHANGED: 'changed',
  UNCHANGED: 'unchanged',
  EDITED: 'edited',
  PENDING: 'pending',
} as const;

export type AiFilter = (typeof AI_FILTER)[keyof typeof AI_FILTER];

export const AI_FILTER_OPTIONS: { value: AiFilter; label: string }[] = [
  { value: AI_FILTER.CHANGED, label: 'Đổi bậc' },
  { value: AI_FILTER.UNCHANGED, label: 'Giữ nguyên bậc' },
  { value: AI_FILTER.EDITED, label: 'Tổ trưởng đã sửa' },
  { value: AI_FILTER.PENDING, label: 'Đang có đề xuất chờ' },
];

/** Số ngày lấy dữ liệu khi tạo gợi ý (khớp BE GradeSuggestionLimits). */
export const AI_LOOKBACK = { MIN: 7, MAX: 90, DEFAULT: 90 } as const;

export const AI_LABELS = {
  lookbackBefore: 'Lấy dữ liệu',
  lookbackAfter: 'ngày gần nhất',
  open: 'AI gợi ý bậc cả tổ',
  title: 'AI gợi ý bậc thợ cho cả tổ',
  generate: 'Tạo gợi ý',
  regenerate: 'Tạo lại',
  regenerateTitle: 'Tạo lại gợi ý?',
  regenerateBody:
    'AI sẽ phân tích lại số liệu {0} ngày gần nhất và thay toàn bộ gợi ý hiện tại, kể cả các dòng bạn đã sửa.',
  generating: 'AI đang phân tích số liệu của cả tổ, có thể mất tới một phút...',
  emptyHint:
    'Chưa có gợi ý nào còn hạn. Chọn số ngày lấy dữ liệu rồi bấm Tạo gợi ý. AI sẽ dựa vào ngày công, giờ làm, sản lượng, độ khó công đoạn và số lỗi của từng công nhân để gợi ý bậc. Hệ thống chỉ gửi số liệu, không gửi tên hay mã nhân viên.',
  period: 'Số liệu từ {0} đến {1}',
  generatedAt: 'Tạo lúc {0} bởi {1}',
  expiresAt: 'Lưu đến {0}',
  confirmedAt: 'Đã xác nhận lúc {0}',
  confirm: 'Xác nhận gửi đề xuất ({0})',
  confirmTitle: 'Gửi đề xuất bậc thợ?',
  confirmBody:
    'Sẽ gửi {0} đề xuất cho công nhân có bậc khác bậc hiện tại ({1} đề xuất đang chờ sẽ được thay bằng bản mới). Công nhân giữ nguyên bậc không được gửi.',
  skippedTitle: 'Một số dòng không được gửi',
  filterAll: 'Mọi dòng',
  filterGrade: 'Bậc đề xuất',
  metrics: 'Số liệu kỳ',
  aiGrade: 'AI gợi ý',
  change: 'Thay đổi',
  changed: 'Đổi bậc',
  unchanged: 'Giữ nguyên',
  edited: 'Đã sửa',
  pendingTag: 'Đang chờ: {0}',
  workDays: 'Ngày công: {0}',
  output: 'Sản lượng: {0} ({1} công đoạn)',
  efficiency: 'Hiệu suất: {0}%',
  defects: 'Lỗi: sửa {0}, hỏng {1}',
  defectRate: ' ({0}%)',
  hours: 'Giờ làm: {0} (tăng ca {1})',
  tenure: 'Thâm niên: {0} tháng',
  skill: 'Độ khó TB: {0}',
  aiReason: 'AI: {0}',
  remove: 'Bỏ gợi ý này',
  removeTitle: 'Bỏ gợi ý của {0}?',
  removeBody: 'Công nhân này sẽ không có trong lần gửi đề xuất. Muốn có lại thì bấm Tạo lại.',
} as const;

export const AI_MESSAGES = {
  generated: 'AI đã gợi ý bậc cho cả tổ.',
  confirmed: 'Đã gửi {0} đề xuất bậc ({1} mới, {2} thay đề xuất cũ).',
  noChange: 'Không có công nhân nào đổi bậc nên không có đề xuất nào được gửi.',
  removed: 'Đã bỏ gợi ý của công nhân này.',
} as const;
