import { REASON_MAX_LENGTH } from '@/constants/limits';
import type { DefectReason, ShiftCode } from '../types';
import { OPERATIONS_TEXT, PIECE_RATES_TEXT } from './operationText';
import { ORDER_LINE_TEAMS_TEXT, ASSIGNMENTS_TEXT } from './assignmentText';
import { OUTPUT_TEXT, QC_TEXT } from './outputText';
import { DEFECTS_TEXT, REPLACEMENT_TEXT, MY_DEFECTS_TEXT } from './defectText';

/** Số lượng hàng hỏng tối đa mỗi phiếu và độ dài ghi chú / lý do / trả lời, khớp DefectLimits ở BE. */
export const DEFECT_MAX_QTY = 1_000_000;
export const DEFECT_TEXT_MAX = REASON_MAX_LENGTH;
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

export const SEWING_TEAM_TYPE = 'SEWING';

/** Chuỗi hiển thị của luồng công đoạn, đơn giá, phân công, sản lượng, QC, hàng hỏng (KI-01..KI-11). Lỗi nghiệp vụ lấy message từ BE.
 * Chuỗi từng màn nằm ở các file *Text.ts cùng thư mục. */
export const PRODUCTION_TEXT = {
  saved: 'Đã lưu.',
  required: 'Vui lòng nhập thông tin này.',
  selectProduct: 'Chọn mã hàng',
  selectOrder: 'Chọn đơn hàng',
  selectOrderLine: 'Chọn mã hàng và màu - size',
  selectTeam: 'Chọn tổ may',
  selectTeamFirst: 'Chọn tổ may trước',
  selectWorker: 'Chọn công nhân',
  selectOperation: 'Chọn công đoạn',
  selectFirst: 'Chọn đơn hàng trước.',
  operations: OPERATIONS_TEXT,
  pieceRates: PIECE_RATES_TEXT,
  orderLineTeams: ORDER_LINE_TEAMS_TEXT,
  assignments: ASSIGNMENTS_TEXT,
  output: OUTPUT_TEXT,
  qc: QC_TEXT,
  defects: DEFECTS_TEXT,
  replacement: REPLACEMENT_TEXT,
  myDefects: MY_DEFECTS_TEXT,
} as const;

/** BE trả ca dạng "MORNING" hoặc "Morning": quy về nhãn tiếng Việt. */
export const shiftText = (shift: string | null | undefined): string => {
  if (!shift) return '—';
  const key = (shift.charAt(0).toUpperCase() + shift.slice(1).toLowerCase()) as ShiftCode;
  return PRODUCTION_TEXT.output.shiftLabel[key] ?? shift;
};

/** Quy ca BE trả ("MORNING" hoặc "Morning") về giá trị gửi lên API. */
export const toShiftCode = (shift: string): ShiftCode =>
  (shift.charAt(0).toUpperCase() + shift.slice(1).toLowerCase()) as ShiftCode;

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
