import type { OutputGridRow } from '../types';
import {
  buildOutputWorkers,
  canTransfer,
  excessOf,
  filterOutputWorkers,
  groupOutputRows,
  OUTPUT_WORKER_STATE,
  remainingOf,
} from './outputWorkerBoard';

const row = (over: Partial<OutputGridRow>): OutputGridRow =>
  ({
    operationId: 1,
    workerId: 7,
    workerCode: 'CN7',
    workerName: 'Nguyễn An',
    quantity: null,
    rateApproved: true,
    noAttendanceWarning: false,
    capQty: 10,
    reportedQty: 0,
    transferableQty: 0,
    ...over,
  }) as OutputGridRow;

describe('outputWorkerBoard', () => {
  it('phần vượt chuyển được khi không quá số người cùng công đoạn và phần tổ chưa giao còn dư', () => {
    const r = row({ capQty: 100, reportedQty: 30, quantity: 10, transferableQty: 25 });

    expect(excessOf(r, 80)).toBe(0);
    expect(excessOf(r, 100)).toBe(20);
    expect(canTransfer(r, 105)).toBe(true);
    expect(canTransfer(r, 106)).toBe(false);
  });

  it('gộp theo công nhân, giữ cả người chưa có công đoạn', () => {
    const workers = buildOutputWorkers(
      [row({ operationId: 1, quantity: 5 }), row({ operationId: 2 })],
      [
        { value: 7, label: 'CN7 - Nguyễn An' },
        { value: 8, label: 'CN8 - Lê Bình' },
      ],
    );

    expect(workers.map((w) => [w.workerId, w.rows.length, w.enteredCount, w.totalQty])).toEqual([
      [7, 2, 1, 5],
      [8, 0, 0, 0],
    ]);
  });

  it('chỉ lấy công đoạn đã giao việc; dòng đã nhập từ trước vẫn giữ', () => {
    const [worker] = buildOutputWorkers(
      [
        row({ operationId: 1 }),
        row({ operationId: 2, capQty: 0 }),
        row({ operationId: 3, capQty: 0, quantity: 4 }),
      ],
      [],
    );

    expect(worker?.rows.map((r) => r.operationId)).toEqual([1, 3]);
  });

  it('tình trạng nhập không tính công đoạn chưa giao việc, chưa có đơn giá', () => {
    const [worker] = buildOutputWorkers(
      [
        row({ operationId: 1, quantity: 5 }),
        row({ operationId: 2, capQty: 0 }),
        row({ operationId: 3, rateApproved: false }),
      ],
      [],
    );

    expect(worker?.enterableCount).toBe(1);
    expect(filterOutputWorkers(worker ? [worker] : [], '', OUTPUT_WORKER_STATE.DONE)).toHaveLength(
      1,
    );
  });

  it('thêm người có sản lượng nhưng đã rời tổ', () => {
    const workers = buildOutputWorkers(
      [row({ workerId: 9, workerCode: 'CN9', workerName: 'Cũ' })],
      [],
    );
    expect(workers[0]?.label).toBe('CN9 - Cũ');
  });

  it('lọc theo từ khóa không dấu và tình trạng nhập', () => {
    const workers = buildOutputWorkers(
      [
        row({ workerId: 7, quantity: 5 }),
        row({ workerId: 8, workerCode: 'CN8', workerName: 'Lê Bình' }),
      ],
      [],
    );

    expect(
      filterOutputWorkers(workers, 'nguyen', OUTPUT_WORKER_STATE.ALL).map((w) => w.workerId),
    ).toEqual([7]);
    expect(
      filterOutputWorkers(workers, '', OUTPUT_WORKER_STATE.DONE).map((w) => w.workerId),
    ).toEqual([7]);
    expect(
      filterOutputWorkers(workers, '', OUTPUT_WORKER_STATE.MISSING).map((w) => w.workerId),
    ).toEqual([8]);
  });

  it('chia nhóm giữ thứ tự, công đoạn chưa có nhóm ở cuối', () => {
    const groups = groupOutputRows([
      row({ operationId: 1, groupName: null }),
      row({ operationId: 2, groupName: 'Túi' }),
      row({ operationId: 3, groupName: 'Tay' }),
      row({ operationId: 4, groupName: 'Túi' }),
    ]);

    expect(groups.map((g) => [g.name, g.rows.map((r) => r.operationId)])).toEqual([
      ['Túi', [2, 4]],
      ['Tay', [3]],
      [null, [1]],
    ]);
  });

  it('số còn được báo không tính số đã nhập của chính dòng, không âm', () => {
    expect(remainingOf(row({ capQty: 100, reportedQty: 90, quantity: 10 }))).toBe(20);
    expect(remainingOf(row({ capQty: 5, reportedQty: 9, quantity: null }))).toBe(0);
  });
});
