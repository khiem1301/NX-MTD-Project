import { normalizeText } from '@/utils/text';
import type { Operation, OperationStatus } from '../types';

/** Giá trị lọc "Chưa có nhóm", không trùng mã nhóm nào. */
export const NO_GROUP = -1;

export const RATE_STATE = {
  MISSING: 'missing',
  DRAFT: 'draft',
  PENDING: 'pending',
  REJECTED: 'rejected',
} as const;
export type RateState = (typeof RATE_STATE)[keyof typeof RATE_STATE];

export type OperationFilter = {
  keyword?: string;
  groupId?: number;
  rateState?: RateState;
  status?: OperationStatus;
};

const RATE_STATE_OF_STATUS = {
  Draft: RATE_STATE.DRAFT,
  PendingApproval: RATE_STATE.PENDING,
  Rejected: RATE_STATE.REJECTED,
} as const;

/** Công đoạn đang dùng mà chưa có đơn giá được duyệt: tổ chưa nhập sản lượng được. */
const isMissingRate = (o: Operation) => o.status === 'Active' && o.currentRate === null;

const matchesRate = (o: Operation, state: RateState) =>
  state === RATE_STATE.MISSING
    ? isMissingRate(o)
    : o.nextRate?.status !== undefined && o.nextRate.status !== 'Approved'
      ? RATE_STATE_OF_STATUS[o.nextRate.status] === state
      : false;

export const filterOperations = (
  operations: readonly Operation[],
  { keyword, groupId, rateState, status }: OperationFilter,
): Operation[] => {
  const needle = normalizeText(keyword?.trim() ?? '');
  return operations.filter(
    (o) =>
      (groupId === undefined ||
        (groupId === NO_GROUP ? o.operationGroupId === null : o.operationGroupId === groupId)) &&
      (rateState === undefined || matchesRate(o, rateState)) &&
      (status === undefined || o.status === status) &&
      (needle === '' ||
        normalizeText(`${o.operationCode} ${o.operationName} ${o.groupName ?? ''}`).includes(
          needle,
        )),
  );
};

export type OperationCounts = {
  active: number;
  ungrouped: number;
  missing: number;
  draft: number;
  pending: number;
  rejected: number;
};

/** Số liệu cho dòng tóm tắt và bộ lọc. */
export const countOperations = (operations: readonly Operation[]): OperationCounts => {
  const countRate = (state: RateState) => operations.filter((o) => matchesRate(o, state)).length;
  return {
    active: operations.filter((o) => o.status === 'Active').length,
    ungrouped: operations.filter((o) => o.operationGroupId === null).length,
    missing: countRate(RATE_STATE.MISSING),
    draft: countRate(RATE_STATE.DRAFT),
    pending: countRate(RATE_STATE.PENDING),
    rejected: countRate(RATE_STATE.REJECTED),
  };
};

/** Mã công đoạn gợi ý khi thêm mới: giống mã sinh khi nhập Excel (STT 7 thành OP07), nhập lại file sẽ khớp đúng công đoạn. */
export const nextOperationDefaults = (operations: readonly Operation[]) => {
  const stationSequence = Math.max(0, ...operations.map((o) => o.stationSequence)) + 1;
  return { stationSequence, operationCode: `OP${String(stationSequence).padStart(2, '0')}` };
};

/** Đơn giá Nháp của các công đoạn đang dùng đã tích, để gửi duyệt một lần. */
export const draftRateIdsOf = (operations: readonly Operation[], selected: readonly number[]) =>
  operations.flatMap((o) =>
    selected.includes(o.operationId) && o.status === 'Active' && o.nextRate?.status === 'Draft'
      ? [o.nextRate.pieceRateId]
      : [],
  );
