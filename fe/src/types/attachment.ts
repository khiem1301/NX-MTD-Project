export const ATTACHMENT_OWNER = {
  Request: 'Request',
  PurchaseOrder: 'PurchaseOrder',
  MaterialReceipt: 'MaterialReceipt',
  MaterialReturn: 'MaterialReturn',
  DefectRecord: 'DefectRecord',
  DispatchNote: 'DispatchNote',
  VehicleTrip: 'VehicleTrip',
} as const;
export type AttachmentOwnerType = (typeof ATTACHMENT_OWNER)[keyof typeof ATTACHMENT_OWNER];

export const ATTACHMENT_PURPOSE = {
  Photo: 'Photo',
  DocumentPhoto: 'DocumentPhoto',
  PaymentProof: 'PaymentProof',
  SignedIssueSlip: 'SignedIssueSlip',
  SignedDeliveryNote: 'SignedDeliveryNote',
  FuelReceipt: 'FuelReceipt',
} as const;
export type AttachmentPurpose = (typeof ATTACHMENT_PURPOSE)[keyof typeof ATTACHMENT_PURPOSE];

export type Attachment = {
  attachmentId: number;
  ownerType: AttachmentOwnerType;
  ownerId: number;
  purpose: AttachmentPurpose;
  fileName: string | null;
  contentType: string | null;
  uploadedAt: string;
};

/** Một dòng lịch sử thay đổi (AuditLogDto ở BE). oldValues / newValues là chuỗi JSON. */
export type AuditEntry = {
  auditLogId: number;
  action: 'INSERT' | 'UPDATE' | 'DELETE' | 'APPROVE' | 'REJECT' | 'CONFIRM' | 'LOCK' | 'UNLOCK' | 'LOGIN';
  oldValues: string | null;
  newValues: string | null;
  reason: string | null;
  changedBy: number | null;
  changedByName: string | null;
  changedAt: string;
};
