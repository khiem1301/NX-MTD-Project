import type { DailyAllocationRow } from '../types';
import {
  buildDailyWorkers,
  DAILY_STATE,
  filterDailyWorkers,
  groupDailyRows,
  maxGiveOf,
} from './dailyBoard';

const row = (over: Partial<DailyAllocationRow>): DailyAllocationRow =>
  ({
    workerId: 7,
    orderLineId: 1,
    operationId: 1,
    groupName: null,
    allocatedQty: 0,
    outputQty: 0,
    remainingQty: 0,
    unallocatedQty: 0,
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
});
