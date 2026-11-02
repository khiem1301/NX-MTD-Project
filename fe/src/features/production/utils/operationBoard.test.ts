import type { Operation } from '../types';
import {
  NO_GROUP,
  RATE_STATE,
  countOperations,
  filterOperations,
  nextOperationDefaults,
} from './operationBoard';

const op = (
  operationId: number,
  name: string,
  operationGroupId: number | null,
  groupName: string | null,
) =>
  ({
    operationId,
    operationCode: 'OP' + operationId,
    operationName: name,
    operationGroupId,
    groupName,
  }) as Operation;
const rows = [
  op(1, 'May cổ', 10, 'Cổ áo'),
  op(2, 'May tay', 20, 'Tay'),
  op(3, 'Là hơi', null, null),
];
const ids = (list: Operation[]) => list.map((o) => o.operationId);

describe('filterOperations', () => {
  it('không lọc thì giữ nguyên', () => {
    expect(ids(filterOperations(rows, {}))).toEqual([1, 2, 3]);
  });

  it('lọc theo nhóm', () => {
    expect(ids(filterOperations(rows, { groupId: 20 }))).toEqual([2]);
  });

  it('lọc dòng chưa có nhóm', () => {
    expect(ids(filterOperations(rows, { groupId: NO_GROUP }))).toEqual([3]);
  });

  it('tìm theo mã, tên, tên nhóm, không phân biệt dấu và khoảng trắng hai đầu', () => {
    expect(ids(filterOperations(rows, { keyword: 'OP3' }))).toEqual([3]);
    expect(ids(filterOperations(rows, { keyword: '  la hoi ' }))).toEqual([3]);
    expect(ids(filterOperations(rows, { keyword: 'cổ áo' }))).toEqual([1]);
  });

  it('kết hợp nhóm và từ khóa', () => {
    expect(filterOperations(rows, { groupId: 10, keyword: 'tay' })).toEqual([]);
  });
});

const rated = (operationId: number, over: Partial<Operation>) =>
  ({
    operationId,
    stationSequence: operationId,
    status: 'Active',
    currentRate: 100,
    nextRate: null,
    operationGroupId: 1,
    ...over,
  }) as Operation;
const next = (status: 'Draft' | 'PendingApproval' | 'Rejected' | 'Approved') =>
  ({
    pieceRateId: 1,
    rate: 1,
    effectiveDate: '2026-11-10',
    status,
    rejectReason: null,
  }) as Operation['nextRate'];
const withRates = [
  rated(1, { currentRate: null }),
  rated(2, { nextRate: next('Draft') }),
  rated(3, { currentRate: null, nextRate: next('PendingApproval') }),
  rated(4, { nextRate: next('Rejected') }),
  rated(5, { nextRate: next('Approved') }),
  rated(6, { currentRate: null, status: 'Discontinued', operationGroupId: null }),
];

describe('lọc theo đơn giá', () => {
  it.each([
    [RATE_STATE.MISSING, [1, 3]],
    [RATE_STATE.DRAFT, [2]],
    [RATE_STATE.PENDING, [3]],
    [RATE_STATE.REJECTED, [4]],
  ])('%s', (rateState, expected) => {
    expect(ids(filterOperations(withRates, { rateState }))).toEqual(expected);
  });

  it('công đoạn đã ngừng không tính là thiếu đơn giá', () => {
    expect(countOperations(withRates)).toEqual({
      active: 5,
      ungrouped: 1,
      missing: 2,
      draft: 1,
      pending: 1,
      rejected: 1,
    });
  });
});

describe('lọc theo trạng thái', () => {
  it.each([
    ['Active', [1, 2, 3, 4, 5]],
    ['Discontinued', [6]],
    [undefined, [1, 2, 3, 4, 5, 6]],
  ] as const)('%s', (status, expected) => {
    expect(ids(filterOperations(withRates, { status }))).toEqual(expected);
  });
});

describe('nextOperationDefaults', () => {
  it('mã hàng chưa có công đoạn thì bắt đầu từ 1', () => {
    expect(nextOperationDefaults([])).toEqual({ stationSequence: 1, operationCode: 'OP01' });
  });

  it('lấy thứ tự lớn nhất cộng 1, kể cả công đoạn đã ngừng', () => {
    expect(nextOperationDefaults(withRates)).toEqual({ stationSequence: 7, operationCode: 'OP07' });
    expect(nextOperationDefaults([rated(120, {})]).operationCode).toBe('OP121');
  });
});
