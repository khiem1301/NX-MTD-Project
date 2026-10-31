import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import type { ProductBlock } from '../utils/orderGrid';
import { ProductBlockEditor } from './ProductBlockEditor';

jest.mock('@/hooks/useOrderLookups', () => ({
  useProductLookup: () => ({
    data: { items: [{ productId: 1, productCode: 'AO-01', productName: 'Áo' }] },
  }),
  useProductColors: () => ({ data: ['Be', 'Đen', 'Xanh'] }),
}));

const oldBlock: ProductBlock = {
  key: 'b',
  productId: 1,
  cmtUnitPrice: 100,
  sizes: ['S', 'M'],
  rows: [
    {
      key: 'r1',
      color: 'Đỏ',
      cells: { S: { orderLineId: 1, qty: 10 }, M: { orderLineId: 2, qty: 20 } },
    },
  ],
};

const renderEditor = (locked: boolean) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ProductBlockEditor
        block={oldBlock}
        locked={locked}
        productLabels={{ 1: 'AO-01 · Áo' }}
        onChange={jest.fn()}
        onRemove={jest.fn()}
        onCreateProduct={jest.fn()}
      />
    </QueryClientProvider>,
  );

describe('ProductBlockEditor', () => {
  it('đơn đã vào sản xuất: không xóa được hàng, size có dòng cũ; ô màu bị khóa; vẫn thêm được màu', () => {
    renderEditor(true);

    expect(screen.queryByLabelText('Xóa')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Xóa size S')).not.toBeInTheDocument();
    expect(screen.getByDisplayValue('Đỏ')).toBeDisabled();
    expect(screen.getByText('Thêm màu')).toBeInTheDocument();
  });

  it('đơn mới: xóa được hàng và size', () => {
    renderEditor(false);

    // antd Table có cuộn ngang render tiêu đề 2 lần nên dùng getAll*.
    expect(screen.getAllByLabelText('Xóa size S').length).toBeGreaterThan(0);
    expect(screen.getAllByLabelText('Xóa').length).toBeGreaterThan(0);
    expect(screen.getByDisplayValue('Đỏ')).not.toBeDisabled();
  });

  it('gõ "đ" vào ô màu gợi ý màu Đen đã khai ở mã hàng khác', async () => {
    function Stateful() {
      const [block, setBlock] = useState(oldBlock);
      return (
        <QueryClientProvider client={new QueryClient()}>
          <ProductBlockEditor
            block={block}
            locked={false}
            productLabels={{ 1: 'AO-01 · Áo' }}
            onChange={setBlock}
            onRemove={jest.fn()}
            onCreateProduct={jest.fn()}
          />
        </QueryClientProvider>
      );
    }
    render(<Stateful />);

    const input = screen.getByDisplayValue('Đỏ');
    // fireEvent thay cho userEvent.type: gõ từng phím quá chậm trên máy CI (vượt timeout 20s)
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: 'đ' } });

    expect((await screen.findAllByText('Đen')).length).toBeGreaterThan(0);
    expect(screen.queryByText('Be')).not.toBeInTheDocument();
  });
});
