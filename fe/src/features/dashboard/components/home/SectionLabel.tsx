import type { ReactNode } from 'react';
import { Typography } from 'antd';

/** Nhãn nhỏ in hoa đầu mỗi khối của trang chủ. */
export function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <Typography.Text
      type="secondary"
      strong
      style={{ display: 'block', margin: '4px 0 10px', fontSize: 12, textTransform: 'uppercase' }}
    >
      {children}
    </Typography.Text>
  );
}
