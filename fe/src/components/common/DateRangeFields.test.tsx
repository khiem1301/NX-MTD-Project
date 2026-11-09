import { fireEvent, render, screen } from '@testing-library/react';
import dayjs from 'dayjs';
import { AppDateRangeFields, type DateRangeValue } from './DateInputs';

const type = (label: string, text: string) => {
  const input = screen.getByLabelText(label);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', keyCode: 13, charCode: 13 });
};

describe('AppDateRangeFields', () => {
  it('có hai ô nhập riêng Từ ngày và Đến ngày, hiện đúng giá trị', () => {
    render(<AppDateRangeFields value={[dayjs('2026-11-01'), dayjs('2026-11-07')]} />);

    expect(screen.getByLabelText('Từ ngày')).toHaveValue('01/11/2026');
    expect(screen.getByLabelText('Đến ngày')).toHaveValue('07/11/2026');
  });

  it('chỉ báo ra ngoài khi đã chọn đủ hai đầu', () => {
    const onChange = jest.fn();
    render(<AppDateRangeFields onChange={onChange} />);

    type('Từ ngày', '01/11/2026');
    expect(onChange).not.toHaveBeenCalled();

    type('Đến ngày', '07/11/2026');
    const [from, to] = (onChange.mock.calls[0] as [DateRangeValue])[0];
    expect([from?.format('YYYY-MM-DD'), to?.format('YYYY-MM-DD')]).toEqual([
      '2026-11-01',
      '2026-11-07',
    ]);
  });

  it('allowPartial báo ngay khi mới chọn một đầu', () => {
    const onChange = jest.fn();
    render(<AppDateRangeFields allowPartial onChange={onChange} />);

    type('Từ ngày', '01/11/2026');

    const [from, to] = (onChange.mock.calls[0] as [DateRangeValue])[0];
    expect([from?.format('YYYY-MM-DD'), to]).toEqual(['2026-11-01', null]);
  });

  it('đổi Từ ngày sang sau Đến ngày thì Đến ngày kéo theo', () => {
    const onChange = jest.fn();
    render(
      <AppDateRangeFields value={[dayjs('2026-11-01'), dayjs('2026-11-05')]} onChange={onChange} />,
    );

    type('Từ ngày', '09/11/2026');

    const [from, to] = (onChange.mock.calls[0] as [DateRangeValue])[0];
    expect([from?.format('YYYY-MM-DD'), to?.format('YYYY-MM-DD')]).toEqual([
      '2026-11-09',
      '2026-11-09',
    ]);
  });
});
