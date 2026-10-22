import type { Operation } from './types';

/** Đơn giá đang sửa được của công đoạn: bản Nháp hoặc bản bị từ chối (sửa xong quay về Nháp, gửi duyệt lại). */
export const editableRate = (operation: Operation) =>
  operation.nextRate !== null && (operation.nextRate.status === 'Draft' || operation.nextRate.status === 'Rejected')
    ? operation.nextRate
    : null;

/** Bề rộng hộp thoại: tối đa `px` trên máy tính, co theo màn hình trên điện thoại (KI-14). */
export const modalWidth = (px: number): string => `min(${px}px, calc(100vw - 16px))`;
