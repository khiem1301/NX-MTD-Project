import { useState } from 'react';
import { Table } from 'antd';
import type { TablePaginationConfig, TableProps } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { DEFAULT_PAGE_SIZE } from '@/constants/pagination';
import { fill } from '@/utils/format';
import { PageSizeSelect } from './PageSizeSelect';

type Columns<T> = NonNullable<TableProps<T>['columns']>;

/** Cột thao tác nhận ra theo key `actions` hoặc tiêu đề "Thao tác". */
const isActionColumn = (col: Columns<object>[number]) =>
  col.key === 'actions' || col.title === MESSAGES.common.actions;

/** Cột thao tác luôn ghim bên phải và căn giữa, để bảng rộng không phải cuộn ngang mới bấm được nút. */
const withActionColumn = <T,>(columns: TableProps<T>['columns']) =>
  columns?.map((col) =>
    isActionColumn(col as Columns<object>[number])
      ? { fixed: 'right' as const, align: 'center' as const, ...col }
      : col,
  );

const showTotal = (total: number, [from, to]: [number, number]) =>
  fill(MESSAGES.common.tableTotal, from, to, total);

/**
 * Trang và số dòng mỗi trang. Bảng phân trang ở máy chủ tự giữ `pageSize` / `current` (useTableQuery) thì dùng của
 * nó; bảng phân trang trên trình duyệt thì DataTable tự giữ. Đổi số dòng thì về trang 1.
 */
const usePaging = (pagination: TablePaginationConfig | undefined) => {
  const [local, setLocal] = useState({ current: 1, pageSize: DEFAULT_PAGE_SIZE });
  const controlled = pagination?.pageSize !== undefined;
  const pageSize = controlled ? (pagination?.pageSize as number) : local.pageSize;
  const change = (page: number, size: number) => {
    if (!controlled) setLocal({ current: size === local.pageSize ? page : 1, pageSize: size });
    pagination?.onChange?.(size === pageSize ? page : 1, size);
  };
  const config: TablePaginationConfig = {
    showTotal,
    ...pagination,
    ...(controlled ? {} : { current: local.current, pageSize: local.pageSize }),
    showSizeChanger: false,
    onChange: change,
  };
  return { config, pageSize, setPageSize: (size: number) => change(1, size) };
};

/**
 * Bảng dữ liệu dùng chung: cỡ nhỏ, cuộn ngang khi màn hẹp, cột "Thao tác" ghim phải, phân trang có tổng số dòng
 * và ô chọn số dòng mỗi trang ở góc trên bên phải. Mọi thuộc tính của antd Table vẫn truyền được và ghi đè mặc định.
 * Dùng `DataTable.Summary` cho dòng tổng. Text khi trống lấy từ ConfigProvider (app/providers.tsx).
 */
export function DataTable<T extends object>({
  columns,
  pagination,
  title,
  ...props
}: TableProps<T>) {
  const paging = usePaging(pagination === false ? undefined : pagination);
  const paged = pagination !== false;
  const header = paged
    ? (rows: readonly T[]) => (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', margin: '0 -8px' }}>
          <div style={{ flex: 1 }}>{title?.(rows)}</div>
          <PageSizeSelect value={paging.pageSize} onChange={paging.setPageSize} />
        </div>
      )
    : title;
  return (
    <Table<T>
      size="small"
      scroll={{ x: 'max-content' }}
      columns={withActionColumn(columns)}
      pagination={paged ? paging.config : false}
      title={header}
      {...props}
    />
  );
}

DataTable.Summary = Table.Summary;
