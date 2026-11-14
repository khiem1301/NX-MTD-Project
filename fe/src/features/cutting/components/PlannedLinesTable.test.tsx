import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Form } from 'antd';
import type { OrderLineOption } from '@/types/lookup';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import { PlannedLinesTable } from './PlannedLinesTable';

const line = (orderedQty: number, plannedQty: number) =>
  ({ orderLineId: 1, color: 'Xanh', sizeCode: 'M', orderedQty, plannedQty }) as OrderLineOption;

const renderTable = (lines: OrderLineOption[], ownPlanned?: Record<string, number>) =>
  render(
    <Form>
      <PlannedLinesTable lines={lines} ownPlanned={ownPlanned} />
    </Form>,
  );

describe('PlannedLinesTable', () => {
  it('chưa có dòng đơn thì chỉ hiện gợi ý, không hiện bảng trống', () => {
    renderTable([]);

    expect(screen.getByText(LABELS.plannedLinesHint)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('có dòng đơn thì hiện bảng để nhập kế hoạch', () => {
    renderTable([line(10, 0)]);

    expect(screen.getByText('Xanh - M')).toBeInTheDocument();
    expect(screen.queryByText(LABELS.plannedLinesHint)).not.toBeInTheDocument();
  });

  it('hiện số còn lên lệnh được = số đặt − kế hoạch của các lệnh khác', () => {
    renderTable([line(100, 70)]);

    expect(screen.getByText('30')).toBeInTheDocument();
  });

  it('sửa lệnh thì cộng trả lại số kế hoạch của chính lệnh đó', () => {
    renderTable([line(100, 70)], { 1: 20 });

    expect(screen.getByText('50')).toBeInTheDocument();
  });

  it('nhập vượt số còn lên lệnh được thì cảnh báo, không tự cắt số', async () => {
    renderTable([line(100, 70)]);
    const input = screen.getByRole('spinbutton');

    await userEvent.type(input, '45');

    expect(input).toHaveValue('45');
    expect(await screen.findByText(fill(LABELS.overPlannableQty, '30'))).toBeInTheDocument();
  });
});
