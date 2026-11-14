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

  it('gõ vượt trần thì giữ số và cảnh báo, không nhận thêm chữ số', async () => {
    const { input, changes } = setup({ max: 500 });

    await userEvent.type(input, '7000');

    expect(input.value).toBe('700');
    expect(changes.at(-1)).toBe(700);
    expect(screen.getByText('Không được vượt quá 500.')).toBeInTheDocument();
  });

  it('dán số dài hơn trần thì cắt chữ số thừa', async () => {
    const { input, changes } = setup();

    await userEvent.click(input);
    await userEvent.paste('99.999.999');

    expect(input.value).toBe('9.999.999');
    expect(changes.at(-1)).toBe(9_999_999);
  });

  it('trần mặc định 1.000.000: gõ được 2.000.000, không gõ thêm chữ số', async () => {
    const { input, changes } = setup();

    await userEvent.type(input, '20000005');

    expect(input.value).toBe('2.000.000');
    expect(changes.at(-1)).toBe(2_000_000);
    expect(screen.getByText('Không được vượt quá 1.000.000.')).toBeInTheDocument();
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

  it('trần nghiệp vụ (limit) không tự cắt số mà báo lỗi dưới ô', async () => {
    const { input, changes } = setup({ limit: 30 });

    await userEvent.type(input, '45');

    expect(input.value).toBe('45');
    expect(changes.at(-1)).toBe(45);
    expect(screen.getByText('Không được vượt quá 30.')).toBeInTheDocument();
  });

  it('trong trần nghiệp vụ thì không báo lỗi', async () => {
    const { input } = setup({ limit: 30 });

    await userEvent.type(input, '30');

    expect(screen.queryByText('Không được vượt quá 30.')).not.toBeInTheDocument();
  });

  it('grouping=false thì không thêm dấu chấm ngăn nghìn', async () => {
    const { input, changes } = setup({ grouping: false, max: 99_999_999 });

    await userEvent.type(input, '123456');

    expect(input.value).toBe('123456');
    expect(changes.at(-1)).toBe(123456);
  });

  it('hiển thị giá trị có sẵn có dấu chấm', () => {
    const { input } = setup({ value: 600000 });

    expect(input.value).toBe('600.000');
  });
});
