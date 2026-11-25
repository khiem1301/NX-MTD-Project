import type { Attachment } from '@/types/attachment';
import { ATTACHMENT_PURPOSE } from '@/types/attachment';
import type { PurchasePayment } from '../types';

/**
 * DEV-ALL-189: số ảnh chứng từ thanh toán tải lên SAU lần thanh toán gần nhất (chưa có lần nào thì tính mọi ảnh). Mỗi lần thanh toán
 * cần ít nhất một ảnh như vậy; cùng cách tính với kiểm tra của máy chủ.
 */
export const newProofCount = (
  attachments: readonly Pick<Attachment, 'purpose' | 'uploadedAt'>[],
  payments: readonly Pick<PurchasePayment, 'createdAt'>[],
): number => {
  const last = Math.max(0, ...payments.map((p) => Date.parse(p.createdAt) || 0));
  return attachments.filter(
    (a) =>
      a.purpose === ATTACHMENT_PURPOSE.PaymentProof &&
      (last === 0 || Date.parse(a.uploadedAt) > last),
  ).length;
};
