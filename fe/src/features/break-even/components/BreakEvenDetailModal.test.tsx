import { render, screen } from '@testing-library/react';
import type { BreakEvenResultItem, BreakEvenRow } from '../types';
import { BreakEvenDetailModal } from './BreakEvenDetailModal';

const base: BreakEvenResultItem = {
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
};

const row = (labour: BreakEvenResultItem | null): BreakEvenRow => ({
  key: 'k',
  calculatedAt: base.calculatedAt,
  productId: 1,
  productCode: 'AO01',
  finishedQty: 100,
  cmtUnitPrice: 100000,
  revenue: 10000000,
  variableCost: 2000000,
  output: base,
  labour,
});

describe('BreakEvenDetailModal', () => {
  it('so hai cách chia: tỉ lệ, định phí, thặng dư cạnh nhau', () => {
    render(
      <BreakEvenDetailModal
        row={row({
          ...base,
          allocationBasis: 'LabourHours',
          sharePct: 50,
          allocatedFixedCost: 750000,
          surplusValue: 7250000,
        })}
        onClose={jest.fn()}
      />,
    );

    expect(screen.getByText('66.67%')).toBeInTheDocument();
    expect(screen.getByText('50%')).toBeInTheDocument();
    expect(screen.getAllByText(/Theo giờ công/).length).toBeGreaterThan(0);
  });

  it('lần tính cũ chỉ có một cách: cách kia báo không có', () => {
    render(<BreakEvenDetailModal row={row(null)} onClose={jest.fn()} />);

    expect(screen.getAllByText('Lần tính này không có cách chia này.').length).toBeGreaterThan(0);
  });

  it('đơn giá không vượt biến phí: báo không đạt được hòa vốn', () => {
    render(
      <BreakEvenDetailModal
        row={{ ...row(null), output: { ...base, breakEvenQty: null, percentReached: null } }}
        onClose={jest.fn()}
      />,
    );

    expect(screen.getByText(/Không đạt được/)).toBeInTheDocument();
  });
});
