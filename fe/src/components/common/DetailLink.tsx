import type { ReactNode } from 'react';
import { Button } from 'antd';

type DetailLinkProps = { onClick: () => void; children: ReactNode };

/** Cách mở chi tiết thống nhất trong mọi bảng: bấm vào mã / tên ở cột đầu tiên. */
export function DetailLink({ onClick, children }: DetailLinkProps) {
  return (
    <Button type="link" onClick={onClick} style={{ padding: 0, height: 'auto' }}>
      {children}
    </Button>
  );
}
