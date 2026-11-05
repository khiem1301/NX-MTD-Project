import { foldVietnamese } from '@/utils/colorSearch';
import type { OutputGridRow } from '../types';

/** Một dòng bảng công nhân của màn sản lượng: các công đoạn có thể báo trong ca và phần đã nhập. */
export type OutputWorker = {
  workerId: number;
  label: string;
  rows: OutputGridRow[];
  enteredCount: number;
  /** Số công đoạn nhập được hoặc đã nhập; không tính công đoạn chưa có đơn giá, đã báo hết số được giao. */
  enterableCount: number;
  totalQty: number;
  noAttendance: boolean;
};

export const OUTPUT_WORKER_STATE = { ALL: 'all', MISSING: 'missing', DONE: 'done' } as const;
export type OutputWorkerState = (typeof OUTPUT_WORKER_STATE)[keyof typeof OUTPUT_WORKER_STATE];

/** Công đoạn lấy từ màn Giao việc: đã được giao và xác nhận; dòng đã nhập từ trước vẫn hiện để sửa được. */
const isAllocated = (row: OutputGridRow) => row.capQty > 0 || row.quantity !== null;

/** Gộp lưới theo công nhân, chỉ giữ công đoạn đã giao việc. Công nhân chưa được giao vẫn có dòng trong bảng. */
export const buildOutputWorkers = (
  rows: readonly OutputGridRow[],
  members: readonly { value: number; label: string }[],
): OutputWorker[] => {
  const known = new Map(members.map((m) => [m.value, m.label]));
  for (const r of rows)
    if (!known.has(r.workerId)) known.set(r.workerId, `${r.workerCode} - ${r.workerName}`);
  return [...known].map(([workerId, label]) => {
    const own = rows.filter((r) => r.workerId === workerId && isAllocated(r));
    const entered = own.filter((r) => r.quantity !== null);
    return {
      workerId,
      label,
      rows: own,
      enteredCount: entered.length,
      enterableCount: own.filter((r) => r.quantity !== null || canEnter(r)).length,
      totalQty: entered.reduce((sum, r) => sum + (r.quantity ?? 0), 0),
      noAttendance: own.some((r) => r.noAttendanceWarning),
    };
  });
};

export const filterOutputWorkers = (
  workers: readonly OutputWorker[],
  keyword: string,
  state: OutputWorkerState,
): OutputWorker[] => {
  const kw = foldVietnamese(keyword.trim());
  return workers.filter((w) => {
    if (state === OUTPUT_WORKER_STATE.MISSING && !isMissing(w)) return false;
    if (state === OUTPUT_WORKER_STATE.DONE && !isDone(w)) return false;
    return !kw || foldVietnamese(w.label).includes(kw);
  });
};

export const isMissing = (w: OutputWorker) => w.enteredCount < w.enterableCount;
export const isDone = (w: OutputWorker) =>
  w.enterableCount > 0 && w.enteredCount === w.enterableCount;

export type OutputGroup = { key: string; name: string | null; rows: OutputGridRow[] };

/** Chia công đoạn theo nhóm, giữ thứ tự xuất hiện; công đoạn chưa có nhóm gom vào một nhóm riêng ở cuối. */
export const groupOutputRows = (rows: readonly OutputGridRow[]): OutputGroup[] => {
  const groups = new Map<string, OutputGroup>();
  for (const row of rows) {
    const name = row.groupName ?? null;
    const key = name ?? '';
    const group = groups.get(key) ?? { key, name, rows: [] };
    group.rows.push(row);
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => Number(a.name === null) - Number(b.name === null));
};

/** Số còn được báo ở công đoạn: số lần được giao trừ phần đã báo ở các ca khác (không tính dòng đang sửa). */
export const remainingOf = (row: OutputGridRow) =>
  Math.max(0, row.capQty - (row.reportedQty - (row.quantity ?? 0)));

/** Phần báo vượt số còn được báo; 0 = không vượt. */
export const excessOf = (row: OutputGridRow, quantity: number) =>
  Math.max(0, quantity - remainingOf(row));

/** Phần vượt chuyển được hết từ người cùng công đoạn còn dư và phần tổ chưa giao. */
export const canTransfer = (row: OutputGridRow, quantity: number) =>
  excessOf(row, quantity) <= row.transferableQty;

/** Lưu được sản lượng ở công đoạn này: đã có đơn giá duyệt và còn số lần được giao. */
export const canEnter = (row: OutputGridRow) => row.rateApproved && remainingOf(row) > 0;

/** Nhập sản lượng nhóm chỉ điền vào công đoạn lưu được: còn hạn sửa, có đơn giá duyệt, còn số lần được giao. */
export const canFill = (row: OutputGridRow) => row.canEdit && canEnter(row);

/** Công đoạn khớp ô tìm theo mã hoặc tên; `keyword` đã bỏ dấu (foldVietnamese). */
export const matchesOperation = (row: OutputGridRow, keyword: string) =>
  foldVietnamese(`${row.operationCode} ${row.operationName}`).includes(keyword);
