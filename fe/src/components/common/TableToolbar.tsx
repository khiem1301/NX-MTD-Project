import type { ReactNode } from 'react';
import { Space } from 'antd';

type TableToolbarProps = { filters?: ReactNode; actions?: ReactNode };

/** Thanh công cụ phía trên bảng trong tab / khối con: bộ lọc bên trái, nút thao tác bên phải. */
export function TableToolbar({ filters, actions }: TableToolbarProps) {
  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, justifyContent: 'space-between', marginBottom: 16 }}>
      <Space wrap>{filters}</Space>
      <Space wrap>{actions}</Space>
    </div>
  );
}
