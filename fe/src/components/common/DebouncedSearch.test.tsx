import { act, fireEvent, render, screen } from '@testing-library/react';
import { DebouncedSearch, SEARCH_DEBOUNCE_MS } from './DebouncedSearch';

describe('DebouncedSearch', () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it('gõ xong chờ một lúc rồi mới tìm, chỉ gọi một lần với giá trị cuối', () => {
    const onSearch = jest.fn();
    render(<DebouncedSearch placeholder="Tìm" onSearch={onSearch} />);
    const input = screen.getByPlaceholderText('Tìm');

    fireEvent.change(input, { target: { value: 'a' } });
    fireEvent.change(input, { target: { value: 'ab' } });
    act(() => void jest.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 1));
    expect(onSearch).not.toHaveBeenCalled();

    act(() => void jest.advanceTimersByTime(1));
    expect(onSearch).toHaveBeenCalledTimes(1);
    expect(onSearch.mock.calls[0]?.[0]).toBe('ab');
  });

  it('Enter tìm ngay và hủy lần tìm đang chờ', () => {
    const onSearch = jest.fn();
    render(<DebouncedSearch placeholder="Tìm" onSearch={onSearch} />);
    const input = screen.getByPlaceholderText('Tìm');

    fireEvent.change(input, { target: { value: 'xyz' } });
    fireEvent.keyDown(input, { key: 'Enter', code: 'Enter', keyCode: 13, charCode: 13 });
    expect(onSearch).toHaveBeenCalledTimes(1);

    act(() => void jest.advanceTimersByTime(SEARCH_DEBOUNCE_MS * 2));
    expect(onSearch).toHaveBeenCalledTimes(1);
  });
});
