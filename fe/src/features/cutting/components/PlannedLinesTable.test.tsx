import { render, screen } from '@testing-library/react';
import { Form } from 'antd';
import type { OrderLineOption } from '@/types/lookup';
import { LABELS } from '../constants';
import { PlannedLinesTable } from './PlannedLinesTable';

const renderTable = (lines: OrderLineOption[]) =>
  render(
    <Form>
      <PlannedLinesTable lines={lines} />
    </Form>,
  );

describe('PlannedLinesTable', () => {
  it('chưa có dòng đơn thì chỉ hiện gợi ý, không hiện bảng trống', () => {
    renderTable([]);

    expect(screen.getByText(LABELS.plannedLinesHint)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('có dòng đơn thì hiện bảng để nhập kế hoạch', () => {
    renderTable([
      { orderLineId: 1, color: 'Xanh', sizeCode: 'M', orderedQty: 10 } as OrderLineOption,
    ]);

    expect(screen.getByText('Xanh - M')).toBeInTheDocument();
    expect(screen.queryByText(LABELS.plannedLinesHint)).not.toBeInTheDocument();
  });
});
