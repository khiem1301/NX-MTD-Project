import { foldVietnamese } from '@/utils/colorSearch';
import type { DailyAllocationRow, OperationProgress } from '../types';

export const DAILY_STATE = { ALL: 'all', IDLE: 'idle', BUSY: 'busy' } as const;
export type DailyState = (typeof DAILY_STATE)[keyof typeof DAILY_STATE];

export type WorkerOption = { value: number; label: string };

/** Một dòng bảng công nhân: các công đoạn đã phân công hoặc đã giao trong ngày, và số liệu cộng lại. */
export type DailyWorker = {
  workerId: number;
  label: string;
  rows: DailyAllocationRow[];
  /** Số công đoạn đã được giao trong ngày. */
  operationCount: number;
  allocatedQty: number;
  outputQty: number;
  remainingQty: number;
};

export const rowKeyOf = (r: Pick<DailyAllocationRow, 'workerId' | 'orderLineId' | 'operationId'>) =>
  `${r.workerId}-${r.orderLineId}-${r.operationId}`;

/** Gộp theo công nhân của tổ; công nhân chưa có công đoạn nào vẫn có dòng để giao thêm. */
export const buildDailyWorkers = (
  rows: readonly DailyAllocationRow[],
  members: readonly WorkerOption[],
  orderLineId: number | undefined,
): DailyWorker[] =>
  members.map((m) => {
    const own = rows.filter(
      (r) => r.workerId === m.value && (orderLineId === undefined || r.orderLineId === orderLineId),
    );
    const given = own.filter((r) => r.allocatedQty > 0);
    return {
      workerId: m.value,
      label: m.label,
      rows: own,
      operationCount: given.length,
      allocatedQty: given.reduce((s, r) => s + r.allocatedQty, 0),
      outputQty: own.reduce((s, r) => s + r.outputQty, 0),
      remainingQty: own.reduce((s, r) => s + r.remainingQty, 0),
    };
  });

export const filterDailyWorkers = (
  workers: readonly DailyWorker[],
  keyword: string,
  state: DailyState,
): DailyWorker[] => {
  const kw = foldVietnamese(keyword.trim());
  return workers.filter(
    (w) =>
      (state === DAILY_STATE.ALL ||
        (state === DAILY_STATE.IDLE ? w.operationCount === 0 : w.operationCount > 0)) &&
      (!kw || foldVietnamese(w.label).includes(kw)),
  );
};

/** Các dòng đơn có trong bảng, không trùng, theo thứ tự xuất hiện. */
export const dailyOrderLines = (rows: readonly DailyAllocationRow[]) => {
  const seen = new Map<number, DailyAllocationRow>();
  for (const r of rows) if (!seen.has(r.orderLineId)) seen.set(r.orderLineId, r);
  return [...seen.values()];
};

export type DailyGroup = { key: string; name: string | null; rows: DailyAllocationRow[] };

/** Chia công đoạn theo nhóm, giữ thứ tự xuất hiện; công đoạn chưa có nhóm gom vào một nhóm riêng ở cuối. */
export const groupDailyRows = (rows: readonly DailyAllocationRow[]): DailyGroup[] => {
  const groups = new Map<string, DailyGroup>();
  for (const row of rows) {
    const key = row.groupName ?? '';
    const group = groups.get(key) ?? { key, name: row.groupName, rows: [] };
    group.rows.push(row);
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => Number(a.name === null) - Number(b.name === null));
};

/** Công đoạn tổ trưởng thêm ngoài phân công, dựng thành dòng giao việc chưa có số. */
export const extraRowFrom = (
  worker: { workerId: number; label: string },
  op: OperationProgress,
): DailyAllocationRow => {
  const [workerCode = '', ...rest] = worker.label.split(' - ');
  return {
    workerId: worker.workerId,
    workerCode,
    workerName: rest.join(' - '),
    orderLineId: op.orderLineId,
    orderCode: op.orderCode,
    productCode: op.productCode,
    color: op.color,
    sizeCode: op.sizeCode,
    operationId: op.operationId,
    operationCode: op.operationCode,
    operationName: op.operationName,
    groupName: op.groupName,
    isAssigned: false,
    allocatedQty: 0,
    outputQty: 0,
    remainingQty: 0,
    unallocatedQty: op.unallocatedQty,
  };
};

/** Số giao tối đa ở dòng: phần đã giao trong ngày cộng số còn giao được của cả tổ. */
export const maxGiveOf = (row: DailyAllocationRow) => row.allocatedQty + row.unallocatedQty;
