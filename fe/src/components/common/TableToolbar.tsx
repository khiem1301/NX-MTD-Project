import { useContext, type ReactNode } from 'react';
import { Space } from 'antd';
import { InPageCardContext } from './pageCardContext';

type TableToolbarProps = { filters?: ReactNode; actions?: ReactNode };

/** Thanh công cụ phía trên bảng: bộ lọc bên trái, nút thao tác bên phải. Luôn cách bảng 16px. */
export function TableToolbar({ filters, actions }: TableToolbarProps) {
  const inPageCard = useContext(InPageCardContext);
  return (
    <div
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 8,
        justifyContent: 'space-between',
        marginBottom: inPageCard ? 0 : 16,
      }}
    >
      <Space wrap>{filters}</Space>
      <Space wrap>{actions}</Space>
    </div>
  );
}
