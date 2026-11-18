import type { WorkAllocation } from '../types';
import { applySharedReason, buildBatchItems, needsReason } from './confirmBatch';

const row = (id: number, quantity: number) =>
  ({ workAllocationId: id, quantity }) as WorkAllocation;
const rows = [row(1, 60), row(2, 40)];

describe('confirmBatch', () => {
  it('mặc định xác nhận đủ số giao, không lý do', () => {
    expect(buildBatchItems(rows, {})).toEqual([
      { workAllocationId: 1, confirmedQty: 60, discrepancyReason: undefined },
      { workAllocationId: 2, confirmedQty: 40, discrepancyReason: undefined },
    ]);
  });

  it('phiếu lệch số thiếu lý do thì chặn gửi', () => {
    const edits = { 2: { confirmedQty: 35 } };
    expect(needsReason(row(2, 40), edits)).toBe(true);
    expect(buildBatchItems(rows, edits)).toBeNull();
  });

  it('chặn số xác nhận rỗng hoặc vượt số giao', () => {
    expect(buildBatchItems(rows, { 1: { confirmedQty: null } })).toBeNull();
    expect(buildBatchItems(rows, { 1: { confirmedQty: 61, discrepancyReason: 'x' } })).toBeNull();
  });

  it('lý do chung chỉ điền vào phiếu lệch chưa có lý do', () => {
    const edits = applySharedReason(
      [...rows, row(3, 10)],
      { 2: { confirmedQty: 35 }, 3: { confirmedQty: 8, discrepancyReason: 'hỏng 2' } },
      ' thiếu BTP ',
    );
    expect(edits[1]).toBeUndefined();
    expect(edits[2]?.discrepancyReason).toBe('thiếu BTP');
    expect(edits[3]?.discrepancyReason).toBe('hỏng 2');
    expect(buildBatchItems(rows, edits)?.[1]).toEqual({
      workAllocationId: 2,
      confirmedQty: 35,
      discrepancyReason: 'thiếu BTP',
    });
  });
});
