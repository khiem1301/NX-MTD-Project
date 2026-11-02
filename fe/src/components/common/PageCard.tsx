import type { CSSProperties, ReactNode } from 'react';
import { Card, Space, Typography } from 'antd';
import { BackButton } from './BackButton';
import { InPageCardContext } from './pageCardContext';

type PageCardProps = {
  title: ReactNode;
  /** Dòng mô tả phụ dưới tiêu đề. */
  description?: ReactNode;
  /** Nút thao tác chính, luôn nằm góc phải tiêu đề. */
  extra?: ReactNode;
  /** Có thì hiện nút quay lại ở góc trái trên cùng (màn chi tiết / thêm / sửa nhảy sang trang khác). */
  onBack?: () => void;
  style?: CSSProperties;
  children?: ReactNode;
};

/** Khung chung của mọi trang chức năng: Card có tiêu đề, mô tả phụ và nút chính ở góc phải. */
export function PageCard({ title, description, extra, onBack, style, children }: PageCardProps) {
  return (
    <Card
      style={style}
      title={
        <div
          style={{
            whiteSpace: 'normal',
            // Chỉ giãn khi có mô tả phụ, để tiêu đề cao bằng Card thường của các màn khác.
            padding: description ? '8px 0' : undefined,
            display: 'flex',
            gap: 8,
            alignItems: 'center',
          }}
        >
          {onBack && <BackButton onClick={onBack} />}
          <div>
            <div>{title}</div>
            {description && (
              <Typography.Text type="secondary" style={{ fontSize: 14, fontWeight: 'normal' }}>
                {description}
              </Typography.Text>
            )}
          </div>
        </div>
      }
      extra={extra}
    >
      <InPageCardContext.Provider value>
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          {children}
        </Space>
      </InPageCardContext.Provider>
    </Card>
  );
}
