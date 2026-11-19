import { fireEvent, render, screen } from '@testing-library/react';
import { ColorAutoComplete } from './ColorAutoComplete';

const useProductColors = jest.fn();
jest.mock('@/hooks/useOrderLookups', () => ({
  useProductColors: (productId?: number | null) => useProductColors(productId),
}));

const openOptions = () => fireEvent.mouseDown(screen.getByRole('combobox'));

describe('ColorAutoComplete (DEV-ALL-132)', () => {
  beforeEach(() => useProductColors.mockReset());

  it('mã đã có: gợi ý màu của chính mã đó', async () => {
    useProductColors.mockReturnValue({ data: ['Đen', 'Trắng'] });
    render(<ColorAutoComplete productId={7} />);
    openOptions();

    expect(useProductColors).toHaveBeenCalledWith(7);
    await screen.findAllByText('Đen');
    const options = [...document.querySelectorAll('.ant-select-item-option')].map(
      (o) => o.textContent,
    );
    expect(options).toEqual(['Đen', 'Trắng']);
  });

  it('mã mới chưa có trong hệ thống: không gợi ý màu', () => {
    useProductColors.mockReturnValue({ data: undefined });
    render(<ColorAutoComplete productId={null} extraColors={['Xanh']} />);
    openOptions();

    expect(useProductColors).toHaveBeenCalledWith(null);
    expect(screen.queryByText('Xanh')).not.toBeInTheDocument();
    expect(document.querySelectorAll('.ant-select-item-option')).toHaveLength(0);
  });
});
