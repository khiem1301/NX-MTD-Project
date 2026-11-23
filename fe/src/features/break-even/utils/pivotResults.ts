import type { BreakEvenResultItem, BreakEvenRow } from '../types';

/**
 * Gộp các dòng kết quả (mỗi mã hàng 2 dòng: theo sản lượng, theo giờ công) thành một dòng / mã hàng / lần tính.
 * Lần tính mới nhất đứng trước; trong một lần tính giữ thứ tự mã hàng như BE trả. Lần tính cũ chỉ có một cách thì cách kia null.
 */
export const pivotResults = (items: BreakEvenResultItem[]): BreakEvenRow[] => {
  const rows = new Map<string, BreakEvenRow>();
  for (const item of items) {
    const key = `${item.calculatedAt}|${item.productId}`;
    const row = rows.get(key) ?? {
      key,
      calculatedAt: item.calculatedAt,
      productId: item.productId,
      productCode: item.productCode,
      finishedQty: item.finishedQty,
      cmtUnitPrice: item.cmtUnitPrice,
      revenue: item.revenue,
      variableCost: item.variableCost,
      output: null,
      labour: null,
    };
    if (item.allocationBasis === 'Output') row.output = item;
    else row.labour = item;
    rows.set(key, row);
  }
  return [...rows.values()].sort((a, b) => b.calculatedAt.localeCompare(a.calculatedAt));
};

export type BreakEvenSummary = {
  calculatedAt: string;
  revenue: number;
  variableCost: number;
  fixedCost: number;
  surplusOutput: number;
  surplusLabour: number;
  riskOutput: number;
  riskLabour: number;
};

/** Tổng quan lần tính mới nhất; null khi chưa có dòng nào. */
export const summarize = (rows: BreakEvenRow[]): BreakEvenSummary | null => {
  if (rows.length === 0) return null;
  const latest = rows[0]!.calculatedAt;
  const run = rows.filter((r) => r.calculatedAt === latest);
  const sum = (pick: (r: BreakEvenRow) => number) => run.reduce((s, r) => s + pick(r), 0);
  return {
    calculatedAt: latest,
    revenue: sum((r) => r.revenue),
    variableCost: sum((r) => r.variableCost),
    fixedCost: sum((r) => r.output?.allocatedFixedCost ?? r.labour?.allocatedFixedCost ?? 0),
    surplusOutput: sum((r) => r.output?.surplusValue ?? 0),
    surplusLabour: sum((r) => r.labour?.surplusValue ?? 0),
    riskOutput: run.filter((r) => r.output?.isAtRisk).length,
    riskLabour: run.filter((r) => r.labour?.isAtRisk).length,
  };
};
