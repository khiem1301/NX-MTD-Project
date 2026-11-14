import type { Defect } from '../types';

type DefectLike = Pick<Defect, 'status'>;

/** Tình trạng của phiếu: đã hủy, hoặc còn hiệu lực (đã ghi; phiếu cũ đã "chấp nhận" vẫn tính còn hiệu lực). */
export type DefectState = 'Cancelled' | 'Active';

export const defectStateOf = (d: DefectLike): DefectState =>
  d.status === 'Cancelled' ? 'Cancelled' : 'Active';

/** Tổ trưởng chỉ sửa / hủy phiếu trong 24 giờ kể từ lúc lập (khớp DefectEditWindow ở BE). */
export const DEFECT_EDIT_HOURS = 24;

/**
 * Sửa / hủy được khi phiếu còn hiệu lực và (còn trong 24 giờ, hoặc là Quản đốc). `canEdit` do BE tính. Các ràng buộc khác (kỳ lương
 * khóa, đơn đã đóng, đã có đơn cắt bù) chỉ BE biết nên báo lỗi khi thao tác.
 */
export const canModifyDefect = (d: Pick<Defect, 'canEdit'>): boolean => d.canEdit;
