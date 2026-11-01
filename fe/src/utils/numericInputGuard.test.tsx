import { fireEvent, render, screen } from '@testing-library/react';
import { InputNumber } from 'antd';
import { installNumericInputGuard } from './numericInputGuard';

const setup = (value: number | null = null) => {
  const onChange = jest.fn();
  render(<InputNumber value={value ?? undefined} onChange={onChange} />);
  return { input: screen.getByRole<HTMLInputElement>('spinbutton'), onChange };
};

describe('numericInputGuard', () => {
  beforeAll(installNumericInputGuard);

  it('không nhận chữ, chỉ giữ lại phần số', () => {
    const { input, onChange } = setup();
    fireEvent.input(input, { target: { value: '12a' } });
    expect(input.value).toBe('12');
    expect(onChange).toHaveBeenLastCalledWith(12);
  });

  it('gõ chữ vào ô trống thì ô vẫn trống', () => {
    const { input, onChange } = setup();
    fireEvent.input(input, { target: { value: 'abc' } });
    expect(input.value).toBe('');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('dán chuỗi lẫn chữ và dấu trừ thì chỉ lấy số', () => {
    const { input } = setup();
    fireEvent.input(input, { target: { value: '-1a2b3' } });
    expect(input.value).toBe('123');
  });

  it('giữ dấu chấm và dấu phẩy của số lẻ', () => {
    const { input } = setup();
    fireEvent.input(input, { target: { value: '1.5' } });
    expect(input.value).toBe('1.5');
  });

  it('bôi đen số rồi gõ chữ: ô và giá trị cùng trống, không lệch nhau', () => {
    const { input, onChange } = setup(12);
    fireEvent.input(input, { target: { value: 'a' } });
    expect(input.value).toBe('');
    expect(onChange).toHaveBeenLastCalledWith(null);
  });

  it('bộ gõ đang ghép chữ thì chờ, ghép xong mới lọc', () => {
    const { input } = setup();
    fireEvent.compositionStart(input);
    fireEvent.input(input, { target: { value: '1a' }, isComposing: true });
    expect(input.value).toBe('1a');
    fireEvent.compositionEnd(input);
    expect(input.value).toBe('1');
  });

  it('không đụng tới ô chữ', () => {
    render(<input aria-label="tên" />);
    const text = screen.getByLabelText<HTMLInputElement>('tên');
    fireEvent.input(text, { target: { value: 'abc' } });
    expect(text.value).toBe('abc');
  });
});
