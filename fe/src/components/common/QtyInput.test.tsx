import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { QtyInput, type QtyInputProps } from './QtyInput';

const setup = (props: QtyInputProps = {}) => {
  const changes: (number | null)[] = [];
  function Host() {
    const [value, setValue] = useState<number | null>(props.value ?? null);
    return (
      <QtyInput
        {...props}
        aria-label="qty"
        value={value}
        onChange={(v) => {
          changes.push(v);
          setValue(v);
        }}
      />
    );
  }
  render(<Host />);
  return { input: screen.getByLabelText('qty') as HTMLInputElement, changes };
};

describe('QtyInput', () => {
  it('thêm dấu chấm ngăn nghìn ngay khi gõ', async () => {
    const { input, changes } = setup();

    await userEvent.type(input, '125000');

    expect(input.value).toBe('125.000');
    expect(changes.at(-1)).toBe(125_000);
  });

  it('gõ vượt trần thì ô tự thành số cao nhất', async () => {
    const { input, changes } = setup({ max: 500 });

    await userEvent.type(input, '7000');

    expect(input.value).toBe('500');
    expect(changes.at(-1)).toBe(500);
  });

  it('dán số vượt trần thì ô thành số cao nhất', async () => {
    const { input, changes } = setup();

    await userEvent.click(input);
    await userEvent.paste('99.999.999');

    expect(input.value).toBe('1.000.000');
    expect(changes.at(-1)).toBe(1_000_000);
  });

  it('trần mặc định của hàng chiếc là 1.000.000', async () => {
    const { input, changes } = setup();

    await userEvent.type(input, '9999999');

    expect(input.value).toBe('1.000.000');
    expect(changes.at(-1)).toBe(1_000_000);
  });

  it('ô NVL nhận phần lẻ sau dấu phẩy', async () => {
    const { input, changes } = setup({ decimals: 3, max: 100_000_000 });

    await userEvent.type(input, '1234,5');

    expect(input.value).toBe('1.234,5');
    expect(changes.at(-1)).toBe(1234.5);
  });

  it('ô số nguyên chặn phím dấu phẩy và dấu chấm', async () => {
    const { input, changes } = setup();

    await userEvent.type(input, '12,3.4');

    expect(input.value).toBe('1.234');
    expect(changes.at(-1)).toBe(1234);
  });

  it('hiển thị giá trị có sẵn có dấu chấm', () => {
    const { input } = setup({ value: 600000 });

    expect(input.value).toBe('600.000');
  });
});
