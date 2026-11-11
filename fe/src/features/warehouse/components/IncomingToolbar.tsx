import type { ReactNode } from 'react';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { TableToolbar } from '@/components/common/TableToolbar';

type IncomingToolbarProps = {
  placeholder: string;
  onSearch: (keyword: string) => void;
  /** Các ô chọn lọc riêng của tab (FilterSelect). */
  children?: ReactNode;
};

/** Thanh lọc của một tab NVL chờ nhận: ô tìm kiếm bên trái, các ô chọn lọc bên cạnh. */
export function IncomingToolbar({ placeholder, onSearch, children }: IncomingToolbarProps) {
  return (
    <TableToolbar
      filters={
        <>
          <DebouncedSearch
            allowClear
            placeholder={placeholder}
            style={{ width: 300 }}
            onSearch={onSearch}
          />
          {children}
        </>
      }
    />
  );
}
