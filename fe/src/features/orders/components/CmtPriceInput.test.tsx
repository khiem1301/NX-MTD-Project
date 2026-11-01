import { fireEvent, render, screen } from '@testing-library/react';
import { CmtPriceInput } from './CmtPriceInput';

const setup = (value: number | null) => {
  const onChange = jest.fn();
  render(<CmtPriceInput value={value} onChange={onChange} />);
  return { input: screen.getByRole('spinbutton'), onChange };
};

describe('CmtPriceInput', () => {
  it('hiện giá trị đã có theo kiểu Việt Nam, không thêm số lẻ thừa', () => {
    expect(setup(15000).input).toHaveValue('15.000');
  });

  it('gõ số có phần lẻ thì nhận đúng giá trị', () => {
    const { input, onChange } = setup(null);
    fireEvent.change(input, { target: { value: '12,5' } });
    expect(onChange).toHaveBeenLastCalledWith(12.5);
  });

  it('số âm và số quá nhiều số lẻ được giữ nguyên để báo lỗi, không tự sửa khi rời ô', () => {
    const { input, onChange } = setup(null);
    fireEvent.change(input, { target: { value: '-5' } });
    fireEvent.blur(input);
    expect(onChange).toHaveBeenLastCalledWith(-5);
    fireEvent.change(input, { target: { value: '1,234' } });
    fireEvent.blur(input);
    expect(onChange).toHaveBeenLastCalledWith(1.234);
  });

  it('xóa trắng ô thì trả null, không thành 0', () => {
    const { input, onChange } = setup(15000);
    fireEvent.change(input, { target: { value: '' } });
    expect(onChange).toHaveBeenLastCalledWith(null);
  });
});
