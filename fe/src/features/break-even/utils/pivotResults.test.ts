import type { BreakEvenResultItem } from '../types';
import { pivotResults, summarize } from './pivotResults';

const item = (over: Partial<BreakEvenResultItem>): BreakEvenResultItem => ({
  breakEvenResultId: 1,
  periodId: 1,
  periodMonth: '2026-10',
  productId: 1,
  productCode: 'AO01',
  allocationBasis: 'Output',
  finishedQty: 100,
  cmtUnitPrice: 100000,
  revenue: 10000000,
  variableCost: 2000000,
  allocatedFixedCost: 1000000,
  breakEvenQty: 12.5,
  percentReached: 800,
  surplusValue: 7000000,
  isAtRisk: false,
  calculatedAt: '2026-11-24T09:15:00',
  atRiskWarning: null,
  sharePct: 66.67,
  ...over,
});

describe('pivotResults', () => {
  it('gộp 2 cách chia của cùng mã hàng, cùng lần tính thành một dòng', () => {
    const rows = pivotResults([
      item({ breakEvenResultId: 1, allocationBasis: 'Output' }),
      item({ breakEvenResultId: 2, allocationBasis: 'LabourHours', allocatedFixedCost: 900000 }),
      item({ breakEvenResultId: 3, productId: 2, productCode: 'QU02' }),
    ]);

    expect(rows).toHaveLength(2);
    expect(rows[0]!.output?.breakEvenResultId).toBe(1);
    expect(rows[0]!.labour?.allocatedFixedCost).toBe(900000);
    expect(rows[1]!.labour).toBeNull();
  });

  it('lịch sử: lần tính mới nhất đứng trước', () => {
    const rows = pivotResults([
      item({ calculatedAt: '2026-11-20T08:00:00' }),
      item({ calculatedAt: '2026-11-24T09:15:00' }),
    ]);

    expect(rows.map((r) => r.calculatedAt)).toEqual(['2026-11-24T09:15:00', '2026-11-20T08:00:00']);
  });
});

describe('summarize', () => {
  it('tổng của lần tính mới nhất, đếm mã nguy cơ lỗ theo từng cách', () => {
    const rows = pivotResults([
      item({ allocationBasis: 'Output', surplusValue: 7000000 }),
      item({ allocationBasis: 'LabourHours', surplusValue: 7100000 }),
      item({
        productId: 2,
        revenue: 4000000,
        variableCost: 4500000,
        allocatedFixedCost: 500000,
        surplusValue: -1000000,
        isAtRisk: true,
      }),
      item({
        productId: 2,
        allocationBasis: 'LabourHours',
        revenue: 4000000,
        variableCost: 4500000,
        allocatedFixedCost: 600000,
        surplusValue: -1100000,
        isAtRisk: true,
      }),
      item({ calculatedAt: '2026-11-01T00:00:00', revenue: 999 }),
    ]);

    expect(summarize(rows)).toEqual({
      calculatedAt: '2026-11-24T09:15:00',
      revenue: 14000000,
      variableCost: 6500000,
      fixedCost: 1500000,
      surplusOutput: 6000000,
      surplusLabour: 6000000,
      riskOutput: 1,
      riskLabour: 1,
    });
  });

  it('không có dòng nào thì không có tổng quan', () => {
    expect(summarize([])).toBeNull();
  });
});
