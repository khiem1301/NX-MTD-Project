import type { EndOfOrderOperation } from '../types';

/** Công đoạn khớp từ khóa khi trùng mã / tên công đoạn hoặc mã / tên một công nhân đã báo công đoạn đó (không phân biệt hoa thường). */
export const operationMatches = (o: EndOfOrderOperation, keyword: string): boolean => {
  const k = keyword.trim().toLowerCase();
  if (!k) return true;
  return (
    `${o.operationCode} ${o.operationName}`.toLowerCase().includes(k) ||
    o.lines.some((l) =>
      l.workers.some((w) => `${w.workerCode} ${w.workerName}`.toLowerCase().includes(k)),
    )
  );
};
