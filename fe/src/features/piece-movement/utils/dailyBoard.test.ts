import type { DailyAllocationRow } from '../types';
import {
  buildDailyWorkers,
  componentsText,
  DAILY_STATE,
  fillGive,
  filterDailyWorkers,
  groupDailyRows,
  isOverGive,
  matchesOperation,
  maxGiveOf,
  minGiveOf,
  previousEdits,
} from './dailyBoard';

const row = (over: Partial<DailyAllocationRow>): DailyAllocationRow =>
  ({
    workerId: 7,
    orderLineId: 1,
    operationId: 1,
    groupName: null,
    carriedQty: 0,
    allocatedQty: 0,
    outputQty: 0,
    remainingQty: 0,
    unallocatedQty: 0,
    previousQty: 0,
    unitRate: null,
    rateApproved: false,
    pendingQty: 0,
    components: [],
    ...over,
  }) as DailyAllocationRow;

const members = [
  { value: 7, label: 'CN7 - Nguyễn An' },
  { value: 8, label: 'CN8 - Trần Bình' },
];

describe('dailyBoard', () => {
  it('gộp theo công nhân, giữ cả người chưa được giao việc', () => {
    const workers = buildDailyWorkers(
      [
        row({ operationId: 1, allocatedQty: 30, outputQty: 20, remainingQty: 10 }),
        row({ operationId: 2, allocatedQty: 0 }),
      ],
      members,
      undefined,
    );

    expect(workers).toHaveLength(2);
    expect(workers[0]).toMatchObject({
      operationCount: 1,
      allocatedQty: 30,
      outputQty: 20,
      remainingQty: 10,
    });
    expect(workers[1]).toMatchObject({ workerId: 8, operationCount: 0 });
  });

  it('lọc theo dòng đơn đang chọn', () => {
    const rows = [
      row({ orderLineId: 1, allocatedQty: 5 }),
      row({ orderLineId: 2, operationId: 3, allocatedQty: 9 }),
    ];

    expect(buildDailyWorkers(rows, members, 2)[0]?.allocatedQty).toBe(9);
  });

  it('lọc người chưa được giao và theo tên không dấu', () => {
    const workers = buildDailyWorkers([row({ allocatedQty: 4 })], members, undefined);

    expect(filterDailyWorkers(workers, '', DAILY_STATE.IDLE).map((w) => w.workerId)).toEqual([8]);
    expect(filterDailyWorkers(workers, 'nguyen', DAILY_STATE.ALL).map((w) => w.workerId)).toEqual([
      7,
    ]);
  });

  it('chia theo nhóm, công đoạn chưa có nhóm xuống cuối', () => {
    const groups = groupDailyRows([
      row({ operationId: 1, groupName: null }),
      row({ operationId: 2, groupName: 'Tay' }),
      row({ operationId: 3, groupName: 'Tay' }),
    ]);

    expect(groups.map((g) => [g.name, g.rows.length])).toEqual([
      ['Tay', 2],
      [null, 1],
    ]);
  });

  it('số giao tối đa là phần đã giao trong ngày cộng số còn giao được', () => {
    expect(maxGiveOf(row({ allocatedQty: 12, unallocatedQty: 30 }))).toBe(42);
  });

  it('tồn từ hôm trước tính vào số công đoạn có việc và hạ được số giao tối thiểu', () => {
    const carried = row({ carriedQty: 15, outputQty: 20 });
    const [worker] = buildDailyWorkers([carried], members, undefined);

    expect(worker).toMatchObject({ operationCount: 1, carriedQty: 15, allocatedQty: 0 });
    expect(minGiveOf(carried)).toBe(5);
    expect(minGiveOf(row({ carriedQty: 30, outputQty: 20 }))).toBe(0);
  });

  it('giao như lần trước chỉ điền dòng đã từng giao, đưa về khoảng nhập được', () => {
    const edits = previousEdits([
      row({ operationId: 1, previousQty: 50, allocatedQty: 0, unallocatedQty: 40 }),
      row({ operationId: 2, previousQty: 10, outputQty: 25, unallocatedQty: 100 }),
      row({ operationId: 3, previousQty: 0 }),
    ]);

    expect(edits).toEqual({ '7-1-1': 40, '7-1-2': 25 });
  });

  it('cộng phần chờ công nhân xác nhận theo công nhân', () => {
    const [worker] = buildDailyWorkers(
      [
        row({ operationId: 1, allocatedQty: 30, pendingQty: 10 }),
        row({ operationId: 2, pendingQty: 5 }),
      ],
      members,
      undefined,
    );

    expect(worker?.pendingQty).toBe(15);
  });

  it('ghi thành phần của công đoạn chỉ theo tên (không kèm số chi tiết), tìm được theo tên thành phần', () => {
    const r = row({
      operationCode: 'OP1',
      operationName: 'Tra tay',
      color: 'Đen',
      sizeCode: 'M',
      components: [
        { productComponentId: 1, componentName: 'Tay áo' },
        { productComponentId: 2, componentName: 'Thân sau' },
      ],
    });

    expect(componentsText(r)).toBe('Tay áo, Thân sau');
    expect(matchesOperation(r, 'than sau')).toBe(true);
  });

  it('ô nhóm chỉ nâng lên số giao tối thiểu, không cắt về số tối đa', () => {
    const r = row({ allocatedQty: 10, unallocatedQty: 40, outputQty: 30, carriedQty: 0 });

    expect(fillGive(r, 20)).toBe(30);
    expect(fillGive(r, 90)).toBe(90);
  });

  it('vượt số giao tối đa thì là vượt trần', () => {
    const r = row({ allocatedQty: 10, unallocatedQty: 40 });

    expect(isOverGive(r, 50)).toBe(false);
    expect(isOverGive(r, 51)).toBe(true);
  });
});
