import type { Defect } from '../types';

type DefectLike = Pick<Defect, 'status' | 'workerObjection'>;

/** Tình trạng của phiếu: hủy / đã chấp nhận theo Status; còn lại là chờ quyết định (công nhân đã phản đối) hoặc đã ghi. */
export type DefectState = 'Cancelled' | 'Confirmed' | 'ObjectionPending' | 'Pending';

/** Công nhân đã phản đối mà Quản đốc chưa quyết định (chấp nhận hay hủy phiếu). */
export const hasPendingObjection = (d: DefectLike): boolean =>
  d.status === 'Recorded' && d.workerObjection !== null;

export const defectStateOf = (d: DefectLike): DefectState => {
  if (d.status === 'Cancelled') return 'Cancelled';
  if (d.status === 'Confirmed') return 'Confirmed';
  return hasPendingObjection(d) ? 'ObjectionPending' : 'Pending';
};

/** Công nhân chỉ được phản đối trong 48 giờ kể từ lúc phiếu được ghi (khớp DefectObjectionWindow ở BE). */
export const DEFECT_OBJECTION_HOURS = 48;

/** Còn trong hạn phản đối: tới đúng mốc 48 giờ vẫn được. */
export const isObjectionOpen = (d: Pick<Defect, 'recordedAt'>, now: Date = new Date()): boolean =>
  now.getTime() - new Date(d.recordedAt).getTime() <= DEFECT_OBJECTION_HOURS * 3_600_000;

/** Chấp nhận phiếu được khi phiếu còn ở trạng thái đã ghi và công nhân đã phản đối. */
export const canConfirmDefect = (d: DefectLike): boolean => hasPendingObjection(d);

/** Hủy được khi phiếu còn ở trạng thái đã ghi, và còn hạn (tổ trưởng) hoặc là Quản đốc. */
export const canCancelDefect = (
  d: DefectLike & Pick<Defect, 'canEdit'>,
  isManager: boolean,
): boolean => d.status === 'Recorded' && (isManager || d.canEdit);
