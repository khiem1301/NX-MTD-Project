import type { OperationAssignment, OperationOption } from '../types';
import {
  assignmentGroups,
  assignmentProducts,
  buildAssignmentRows,
  filterAssignmentRows,
  workerLoads,
} from './assignmentBoard';

const op = (operationId: number) => ({ operationId }) as OperationOption;
const assignment = (operationId: number, workerId: number) =>
  ({ operationId, workerId }) as OperationAssignment;

describe('buildAssignmentRows', () => {
  it('mỗi công đoạn một dòng, gom đúng công nhân của công đoạn đó', () => {
    const rows = buildAssignmentRows(
      [op(1), op(2)],
      [assignment(1, 7), assignment(1, 8), assignment(9, 7)],
      new Set([7, 8]),
    );

    expect(rows.map((r) => r.assignments.length)).toEqual([2, 0]);
    expect(rows.map((r) => r.isCovered)).toEqual([true, false]);
  });

  it('công nhân đã nghỉ hoặc chuyển tổ không tính là có người', () => {
    const [row] = buildAssignmentRows([op(1)], [assignment(1, 7)], new Set([8]));

    expect(row?.isCovered).toBe(false);
    expect(row?.assignments).toHaveLength(1);
  });

  it('chỉ cần một công nhân còn làm là đủ', () => {
    const [row] = buildAssignmentRows([op(1)], [assignment(1, 7), assignment(1, 8)], new Set([8]));

    expect(row?.isCovered).toBe(true);
  });
});

describe('bộ lọc công đoạn', () => {
  const named = (
    operationId: number,
    operationCode: string,
    operationName: string,
    groupName: string | null,
  ) => ({ operationId, operationCode, operationName, groupName }) as OperationOption;
  const rows = buildAssignmentRows(
    [
      named(1, 'OP01', 'May cổ áo', 'Cổ'),
      named(2, 'OP02', 'Tra tay', 'Tay'),
      named(3, 'OP03', 'Vắt sổ', null),
    ],
    [assignment(1, 7), assignment(2, 7), assignment(2, 8)],
    new Set([7, 8]),
  );
  const ids = (filter: Parameters<typeof filterAssignmentRows>[1]) =>
    filterAssignmentRows(rows, filter).map((r) => r.operation.operationId);

  it('lọc theo tình trạng', () => {
    expect(ids({ state: 'all' })).toEqual([1, 2, 3]);
    expect(ids({ state: 'missing' })).toEqual([3]);
    expect(ids({ state: 'covered' })).toEqual([1, 2]);
  });

  it('tìm theo mã và tên, không phân biệt dấu và hoa thường', () => {
    expect(ids({ state: 'all', keyword: 'op02' })).toEqual([2]);
    expect(ids({ state: 'all', keyword: 'co ao' })).toEqual([1]);
    expect(ids({ state: 'all', keyword: '  ' })).toEqual([1, 2, 3]);
  });

  it('lọc theo nhóm và công nhân, kết hợp được với nhau', () => {
    expect(ids({ state: 'all', group: 'Tay' })).toEqual([2]);
    expect(ids({ state: 'all', workerId: 7 })).toEqual([1, 2]);
    expect(ids({ state: 'all', workerId: 8, group: 'Cổ' })).toEqual([]);
  });

  it('liệt kê nhóm không trùng, bỏ công đoạn không có nhóm', () => {
    expect(assignmentGroups(rows)).toEqual(['Cổ', 'Tay']);
  });

  it('đếm số công đoạn của từng công nhân', () => {
    expect([...workerLoads(rows)]).toEqual([
      [7, 2],
      [8, 1],
    ]);
  });
});

describe('assignmentProducts', () => {
  it('đếm tổng và số công đoạn đã có người theo từng mã hàng', () => {
    const of = (operationId: number, productId: number, productCode: string) =>
      ({ operationId, productId, productCode }) as OperationOption;
    const rows = buildAssignmentRows(
      [of(1, 3, 'AO'), of(2, 3, 'AO'), of(3, 4, 'QU')],
      [assignment(1, 7)],
      new Set([7]),
    );

    expect(assignmentProducts(rows)).toEqual([
      { productId: 3, productCode: 'AO', total: 2, covered: 1 },
      { productId: 4, productCode: 'QU', total: 1, covered: 0 },
    ]);
  });

  it('mã được chia mà chưa có công đoạn vẫn có mặt với tổng 0, nhiều dòng đơn chỉ tính một mã', () => {
    const rows = buildAssignmentRows(
      [{ operationId: 1, productId: 3, productCode: 'AO' } as OperationOption],
      [assignment(1, 7)],
      new Set([7]),
    );
    const lines = [
      { productId: 3, productCode: 'AO' },
      { productId: 3, productCode: 'AO' },
      { productId: 4, productCode: 'QU' },
    ];

    expect(assignmentProducts(rows, lines)).toEqual([
      { productId: 3, productCode: 'AO', total: 1, covered: 1 },
      { productId: 4, productCode: 'QU', total: 0, covered: 0 },
    ]);
  });
});
