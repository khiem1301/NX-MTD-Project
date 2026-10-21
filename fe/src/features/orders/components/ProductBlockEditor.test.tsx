import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ProductBlock } from '../utils/orderGrid';
import { ProductBlockEditor } from './ProductBlockEditor';

jest.mock('@/hooks/useOrderLookups', () => ({
  useProductLookup: () => ({ data: { items: [{ productId: 1, productCode: 'AO-01', productName: 'Áo' }] } }),
}));

const oldBlock: ProductBlock = {
  key: 'b',
  productId: 1,
  cmtUnitPrice: 100,
  sizes: ['S', 'M'],
  rows: [{ key: 'r1', color: 'Đỏ', cells: { S: { orderLineId: 1, qty: 10 }, M: { orderLineId: 2, qty: 20 } } }],
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
});
