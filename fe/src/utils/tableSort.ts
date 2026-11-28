import type { TablePaginationConfig, TableProps } from 'antd';

type Column<T> = NonNullable<TableProps<T>['columns']>[number];
type DataIndex = (string | number)[] | string | number;

const LOCALE = 'vi';

const valueAt = (row: object, dataIndex: DataIndex): unknown =>
  [dataIndex]
    .flat()
    .reduce<unknown>(
      (value, key) => (value as Record<string | number, unknown> | null)?.[key],
      row,
    );

/** So sánh hai giá trị ô: số theo số, chữ theo tiếng Việt (số trong chữ theo giá trị), ô trống xếp trước. */
export const compareValues = (a: unknown, b: unknown): number => {
  if (a === null || a === undefined || a === '')
    return b === null || b === undefined || b === '' ? 0 : -1;
  if (b === null || b === undefined || b === '') return 1;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  if (typeof a === 'boolean' && typeof b === 'boolean') return Number(a) - Number(b);
  return String(a).localeCompare(String(b), LOCALE, { numeric: true });
};

/**
 * Cho mọi cột có `dataIndex` mà chưa khai báo `sorter` (và không phải cột thao tác) sắp xếp được khi bấm tiêu đề.
 * Sắp xếp trên các dòng bảng đang có; bảng phân trang ở máy chủ thì chỉ sắp trong trang đang xem.
 */
export const withDefaultSorter = <T extends object>(
  columns: TableProps<T>['columns'],
  isAction: (column: Column<T>) => boolean,
): TableProps<T>['columns'] =>
  columns?.map((column): Column<T> => {
    if ('children' in column)
      return { ...column, children: withDefaultSorter(column.children, isAction) };
    if (column.dataIndex === undefined || 'sorter' in column || isAction(column)) return column;
    const dataIndex = column.dataIndex as DataIndex;
    return {
      ...column,
      sorter: (a: T, b: T) => compareValues(valueAt(a, dataIndex), valueAt(b, dataIndex)),
    };
  });

/** Khóa gắn trạng thái sắp xếp phía máy chủ vào cấu hình phân trang (useTableQuery.pagination) để DataTable nhận ra mà không phải sửa từng màn. */
export const TABLE_SORT = Symbol('tableSort');

export type ServerSort = { sortBy?: string; sortDir?: 'asc' | 'desc' };
export type ServerSortState = { sort: ServerSort; onChange: (next: ServerSort) => void };
export type SortablePagination = TablePaginationConfig & { [TABLE_SORT]?: ServerSortState };

/** Đổi kết quả sắp xếp của antd ({ field hoặc columnKey, order }) sang `sortBy` / `sortDir` gửi lên máy chủ; bỏ sắp xếp thì để trống. */
export const toServerSort = (sorter: unknown): ServerSort => {
  const first = (Array.isArray(sorter) ? sorter[0] : sorter) as
    { field?: unknown; columnKey?: unknown; order?: 'ascend' | 'descend' | null } | undefined;
  const key = first?.field ?? first?.columnKey;
  if (!first?.order || typeof key !== 'string') return {};
  return { sortBy: key, sortDir: first.order === 'ascend' ? 'asc' : 'desc' };
};

/**
 * Bảng phân trang ở máy chủ: cột có `dataIndex` dạng chữ (tên cột DTO, máy chủ sắp theo tên đó) bấm tiêu đề để máy chủ sắp xếp
 * trên toàn bộ dữ liệu, không chỉ trang đang xem. Cột hiển thị ghép nhiều dữ liệu (không có `dataIndex`) khai `sorter: true`
 * và `key` là tên cột DTO để sắp theo cột đó.
 */
export const withServerSorter = <T extends object>(
  columns: TableProps<T>['columns'],
  sort: ServerSort,
  isAction: (column: Column<T>) => boolean,
): TableProps<T>['columns'] =>
  columns?.map((column): Column<T> => {
    if ('children' in column)
      return { ...column, children: withServerSorter(column.children, sort, isAction) };
    const key =
      typeof column.dataIndex === 'string'
        ? column.dataIndex
        : column.sorter === true && typeof column.key === 'string'
          ? column.key
          : undefined;
    if (
      key === undefined ||
      isAction(column) ||
      (column.sorter !== undefined && column.sorter !== true)
    )
      return column;
    const active = sort.sortBy?.toLowerCase() === key.toLowerCase();
    return {
      ...column,
      sorter: true,
      sortOrder: active ? (sort.sortDir === 'asc' ? 'ascend' : 'descend') : null,
    };
  });
