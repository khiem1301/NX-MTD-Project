import type {
  OperationExcelRowBody,
  OperationExcelSheetBody,
  OperationImportPreview,
  OperationImportRow,
} from '../types';

/** Dòng công đoạn sửa được trên popup: như dòng gửi máy chủ, trừ cột CUỐI (chọn ở ô "Công đoạn cuối"). */
export type OperationDraftRow = Omit<OperationExcelRowBody, 'isFinal'>;

/** Lỗi do kế hoạch nhập (trùng STT, nhiều công đoạn cuối, STT ngoài giới hạn): máy chủ tính lại mỗi lần kiểm, không giữ làm lỗi ô. */
const PLAN_LEVEL_ERROR = /^(Trùng STT|Cột CUỐI|STT vượt)/;

/**
 * Dòng của bản nháp từ kết quả xem trước file. Dòng lỗi vì ô không đọc được (vd TGCT là chữ) giữ lỗi đó đến khi người dùng sửa dòng;
 * nếu không, máy chủ chỉ thấy giá trị rỗng và coi dòng là hợp lệ.
 */
export const draftFromPreview = (rows: readonly OperationImportRow[]): OperationDraftRow[] =>
  rows.map((r) => ({
    rowNumber: r.rowNumber,
    stt: r.stt,
    name: r.operationName,
    skillLevel: r.skillLevel,
    standardTimeSec: r.standardTimeSec,
    equipment: r.equipment,
    rate: r.rate,
    parseError: r.action === 'Error' && r.error && !PLAN_LEVEL_ERROR.test(r.error) ? r.error : null,
    groupName: r.groupName,
    componentNames: r.componentNames,
  }));

/** Bảng gửi máy chủ kiểm / nhập: thông tin sheet theo kết quả xem trước, dòng theo bản nháp. */
export const sheetBodyOf = (
  base: OperationImportPreview,
  rows: readonly OperationDraftRow[],
): OperationExcelSheetBody => ({
  sheetName: base.sheetName,
  productText: base.productText,
  // Đơn giá bắt buộc nên luôn gửi như có cột TIỀN: file thiếu cột thì nhập tay ở ô Đơn giá của bảng (DEV-ALL-190).
  hasRateColumn: true,
  hasGroupColumn: base.hasGroupColumn,
  hasComponentColumn: base.hasComponentColumn,
  rows: rows.map((r) => ({ ...r, isFinal: false })),
});

/** Dòng trống để thêm: số dòng và STT lớn hơn mọi dòng đang có. */
export const blankDraftRow = (rows: readonly OperationDraftRow[]): OperationDraftRow => ({
  rowNumber: Math.max(0, ...rows.map((r) => r.rowNumber)) + 1,
  stt: Math.max(0, ...rows.map((r) => r.stt)) + 1,
  name: '',
  skillLevel: null,
  standardTimeSec: null,
  equipment: null,
  rate: null,
  parseError: null,
  groupName: null,
  componentNames: null,
});
