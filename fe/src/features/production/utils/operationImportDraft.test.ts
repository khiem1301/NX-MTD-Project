import type { OperationImportPreview, OperationImportRow } from '../types';
import { blankDraftRow, draftFromPreview, sheetBodyOf } from './operationImportDraft';

const row = (patch: Partial<OperationImportRow>): OperationImportRow => ({
  rowNumber: 4,
  stt: 1,
  operationCode: 'OP01',
  operationName: 'Ráp vai',
  skillLevel: 2,
  standardTimeSec: 18,
  equipment: null,
  rate: null,
  action: 'New',
  error: null,
  rateAction: 'None',
  rateNote: null,
  groupName: null,
  componentNames: null,
  changeNote: null,
  warning: null,
  ...patch,
});

describe('draftFromPreview', () => {
  it('giữ lỗi ô lúc đọc file để dòng không thành hợp lệ khi máy chủ chỉ thấy ô rỗng', () => {
    const [draft] = draftFromPreview([
      row({ action: 'Error', error: "Cột TGCT không phải số ('abc').", standardTimeSec: null }),
    ]);
    expect(draft?.parseError).toBe("Cột TGCT không phải số ('abc').");
  });

  it('bỏ lỗi do kế hoạch (trùng STT, nhiều công đoạn cuối) vì máy chủ tính lại mỗi lần kiểm', () => {
    const drafts = draftFromPreview([
      row({ action: 'Error', error: 'Trùng STT 3 trong file.' }),
      row({ rowNumber: 5, action: 'Error', error: 'Cột CUỐI chỉ được đánh x ở một công đoạn.' }),
    ]);
    expect(drafts.map((d) => d.parseError)).toEqual([null, null]);
  });
});

describe('sheetBodyOf / blankDraftRow', () => {
  const base = {
    sheetName: 'Công đoạn',
    productText: 'AO01',
    hasRateColumn: true,
    hasGroupColumn: false,
    hasComponentColumn: true,
  } as OperationImportPreview;

  it('thông tin sheet theo kết quả xem trước, cột CUỐI không gửi (chọn ở ô công đoạn cuối)', () => {
    const body = sheetBodyOf(base, draftFromPreview([row({})]));
    expect(body).toMatchObject({
      sheetName: 'Công đoạn',
      hasRateColumn: true,
      hasComponentColumn: true,
    });
    expect(body.rows[0]?.isFinal).toBe(false);
  });

  it('dòng thêm mới có số dòng và STT lớn hơn mọi dòng đang có', () => {
    const rows = draftFromPreview([row({ rowNumber: 4, stt: 7 }), row({ rowNumber: 9, stt: 3 })]);
    expect(blankDraftRow(rows)).toMatchObject({ rowNumber: 10, stt: 8, name: '' });
  });
});
