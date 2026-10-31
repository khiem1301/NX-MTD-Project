import { Table } from 'antd';
import type { TableProps } from 'antd';

/**
 * Bảng dữ liệu dùng chung: cỡ nhỏ, cuộn ngang khi màn hẹp (điện thoại). Mọi thuộc tính của antd Table vẫn truyền được
 * và ghi đè mặc định. Dùng `DataTable.Summary` cho dòng tổng. Text khi trống lấy từ ConfigProvider (app/providers.tsx).
 */
export function DataTable<T extends object>(props: TableProps<T>) {
  return <Table<T> size="small" scroll={{ x: 'max-content' }} {...props} />;
}

DataTable.Summary = Table.Summary;
