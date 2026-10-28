import { Input } from 'antd';
import { useEffect, useRef, type ComponentProps } from 'react';

type SearchProps = ComponentProps<typeof Input.Search>;

/** Khoảng chờ sau lần gõ cuối trước khi gọi `onSearch` (tìm tức thì, không gửi mỗi phím). */
export const SEARCH_DEBOUNCE_MS = 300;

/**
 * Ô tìm kiếm tức thì: gõ là lọc sau {@link SEARCH_DEBOUNCE_MS}; Enter hoặc nút tìm lọc ngay.
 * Dùng thay cho `Input.Search` với cùng props, nên các trang chỉ đổi tên component.
 */
export function DebouncedSearch({ onSearch, onChange, ...rest }: SearchProps) {
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const latest = useRef(onSearch);

  useEffect(() => {
    latest.current = onSearch;
  }, [onSearch]);
  useEffect(() => () => clearTimeout(timer.current), []);

  return (
    <Input.Search
      {...rest}
      onChange={(e) => {
        onChange?.(e);
        clearTimeout(timer.current);
        const value = e.target.value;
        timer.current = setTimeout(() => latest.current?.(value, undefined, { source: 'input' }), SEARCH_DEBOUNCE_MS);
      }}
      onSearch={(value, event, info) => {
        clearTimeout(timer.current);
        onSearch?.(value, event, info);
      }}
    />
  );
}
