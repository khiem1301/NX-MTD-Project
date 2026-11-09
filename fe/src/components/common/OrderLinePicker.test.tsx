import { fireEvent, render, screen } from '@testing-library/react';
import { OrderLinePicker, type PickableOrderLine } from './OrderLinePicker';

const lines: PickableOrderLine[] = [
  { orderLineId: 1, productCode: 'AO01', color: 'Đỏ', sizeCode: 'M', orderedQty: 100 },
  { orderLineId: 2, productCode: 'AO01', color: 'Xanh', sizeCode: 'L' },
  { orderLineId: 3, productCode: 'QU02', color: 'Đen', sizeCode: 'S' },
];

const combo = (index: number) => screen.getAllByRole('combobox')[index] as HTMLElement;

describe('OrderLinePicker', () => {
  it('chưa chọn mã hàng thì chưa chọn được màu - size', () => {
    render(<OrderLinePicker lines={lines} onChange={jest.fn()} />);

    expect(combo(1)).toBeDisabled();
    expect(screen.getByText('Chọn mã hàng trước')).toBeInTheDocument();
  });

  it('chọn mã hàng thì chỉ hiện màu - size của mã đó, chọn màu - size trả về dòng đơn', () => {
    const onChange = jest.fn();
    render(<OrderLinePicker lines={lines} onChange={onChange} />);

    fireEvent.mouseDown(combo(0));
    fireEvent.click(screen.getByTitle('AO01'));
    expect(onChange).toHaveBeenLastCalledWith(undefined);

    fireEvent.mouseDown(combo(1));
    expect(screen.getByTitle('Đỏ - M (đặt 100)')).toBeInTheDocument();
    expect(screen.queryByTitle('Đen - S')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Xanh - L'));
    expect(onChange).toHaveBeenLastCalledWith(2, expect.anything());
  });

  it('đơn chỉ có một mã hàng thì chọn sẵn mã đó', () => {
    render(<OrderLinePicker lines={lines.slice(0, 2)} onChange={jest.fn()} />);

    expect(combo(1)).not.toBeDisabled();
  });
});
