export type AuditRow = {
  auditLogId: number;
  tableName: string;
  recordId: number;
  action: string;
  oldValues: string | null;
  newValues: string | null;
  reason: string | null;
  /** Câu mô tả tiếng Việt do BE dựng lúc ghi; dòng cũ chưa có thì null. */
  summary: string | null;
  changedBy: number | null;
  changedByName: string | null;
  changedAt: string;
};
