import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { CmtPriceInput } from './CmtPriceInput';

/** Ô đơn giá trong form luôn có giá trị do cha giữ: test cũng điều khiển ô như vậy. */
const setup = (initial: number | null) => {
  const changes: Array<number | null> = [];
  function Host() {
    const [value, setValue] = useState<number | null>(initial);
    return (
      <CmtPriceInput
        value={value}
        onChange={(next) => {
          changes.push(next);
          setValue(next);
        }}
      />
    );
  }
  render(<Host />);
  return { input: screen.getByRole<HTMLInputElement>('spinbutton'), changes };
};

describe('CmtPriceInput', () => {
  it('hiện giá trị đã có theo kiểu Việt Nam, không thêm số lẻ thừa', () => {
    expect(setup(15000).input).toHaveValue('15.000');
  });

  it('dán số có phần lẻ thì bỏ phần lẻ, không ghép vào số nguyên', () => {
    const { input, changes } = setup(null);
    fireEvent.change(input, { target: { value: '1.234,56' } });
    expect(input.value).toBe('1.234');
    expect(changes.at(-1)).toBe(1234);
  });

  it('gõ 1.2 thì ô hiện 12 và giá trị là 12, không thể nhầm là 1,2', () => {
    const { input, changes } = setup(null);
    fireEvent.change(input, { target: { value: '1.2' } });
    expect(input.value).toBe('12');
    expect(changes.at(-1)).toBe(12);
  });

  it('gõ 1200 thì ô tự hiện 1.200', () => {
    const { input, changes } = setup(null);
    fireEvent.change(input, { target: { value: '1200' } });
    expect(input.value).toBe('1.200');
    expect(changes.at(-1)).toBe(1200);
  });

  it('phím dấu chấm và dấu phẩy bị chặn, dấu ngăn nghìn do ô tự thêm', () => {
    const { input } = setup(null);
    expect(fireEvent.keyDown(input, { key: '.' })).toBe(false);
    expect(fireEvent.keyDown(input, { key: ',' })).toBe(false);
    expect(fireEvent.keyDown(input, { key: '5' })).toBe(true);
  });

  it('ô và giá trị luôn khớp nhau: dấu trừ không vào được giá trị', () => {
    const { input, changes } = setup(null);
    fireEvent.change(input, { target: { value: '-5' } });
    expect(input.value).toBe('5');
    expect(changes.at(-1)).toBe(5);
  });

  it('rời ô thì giữ nguyên số đã nhập', () => {
    const { input, changes } = setup(null);
    fireEvent.change(input, { target: { value: '1500000' } });
    fireEvent.blur(input);
    expect(changes.at(-1)).toBe(1500000);
    expect(input.value).toBe('1.500.000');
  });

  it('xóa trắng ô thì trả null, không thành 0', () => {
    const { changes, input } = setup(15000);
    fireEvent.change(input, { target: { value: '' } });
    expect(changes.at(-1)).toBeNull();
  });
});
