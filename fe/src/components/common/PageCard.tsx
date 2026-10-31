import type { CSSProperties, ReactNode } from 'react';
import { Card, Space, Typography } from 'antd';

type PageCardProps = {
  title: ReactNode;
  /** Dòng mô tả phụ dưới tiêu đề. */
  description?: ReactNode;
  /** Nút thao tác chính, luôn nằm góc phải tiêu đề. */
  extra?: ReactNode;
  style?: CSSProperties;
  children?: ReactNode;
};

/** Khung chung của mọi trang chức năng: Card có tiêu đề, mô tả phụ và nút chính ở góc phải. */
export function PageCard({ title, description, extra, style, children }: PageCardProps) {
  return (
    <Card
      style={style}
      title={
        <div style={{ whiteSpace: 'normal', padding: '8px 0' }}>
          <div>{title}</div>
          {description && (
            <Typography.Text type="secondary" style={{ fontSize: 14, fontWeight: 'normal' }}>
              {description}
            </Typography.Text>
          )}
        </div>
      }
      extra={extra}
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        {children}
      </Space>
    </Card>
  );
}
