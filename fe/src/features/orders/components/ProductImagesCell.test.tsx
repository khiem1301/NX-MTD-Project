import { fireEvent, render, screen } from '@testing-library/react';
import type { ProductListItem } from '../types';
import { ProductImagesCell } from './ProductImagesCell';

jest.mock('./ProductImage', () => ({
  ProductImage: ({ value }: { value: string }) => <span data-testid="thumb">{value}</span>,
}));

const product = (change: Partial<ProductListItem>): ProductListItem => ({
  productId: 1,
  productCode: 'AO-01',
  productName: 'Áo',
  sampleImageUrl: 'PRODUCT/2026/11/main.png',
  extraImages: [],
  status: 'Active',
  isUsed: false,
  createdAt: '2026-11-01T00:00:00',
  ...change,
});

describe('ProductImagesCell', () => {
  it('chỉ có ảnh chính thì không hiện nhãn số ảnh phụ', () => {
    render(<ProductImagesCell product={product({})} onOpenGallery={jest.fn()} />);

    expect(screen.getByTestId('thumb')).toHaveTextContent('main.png');
    expect(screen.queryByText(/^\+/)).not.toBeInTheDocument();
  });

  it('có ảnh phụ thì hiện +N và bấm vào để xem tất cả ảnh', () => {
    const onOpen = jest.fn();
    render(
      <ProductImagesCell
        product={product({ extraImages: ['a', 'b', 'c'] })}
        onOpenGallery={onOpen}
      />,
    );

    fireEvent.click(screen.getByText('+3'));

    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('chưa có ảnh chính nhưng có ảnh phụ thì vẫn xem được ảnh phụ', () => {
    render(
      <ProductImagesCell
        product={product({ sampleImageUrl: null, extraImages: ['a'] })}
        onOpenGallery={jest.fn()}
      />,
    );

    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('+1')).toBeInTheDocument();
  });
});
