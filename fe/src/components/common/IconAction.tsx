import type { MouseEventHandler, ReactNode } from 'react';
import { Button, Tooltip } from 'antd';

type IconActionProps = {
  /** Tên thao tác: hiện khi rê chuột và đọc được bằng trình đọc màn hình. */
  title: string;
  icon: ReactNode;
  onClick?: MouseEventHandler<HTMLElement>;
  danger?: boolean;
  disabled?: boolean;
  loading?: boolean;
};

/** Nút thao tác trên một dòng bảng: chỉ có icon để cột thao tác gọn. Bọc được trong `ConfirmAction`. */
export function IconAction({ title, icon, ...buttonProps }: IconActionProps) {
  return (
    <Tooltip title={title}>
      <Button type="text" size="small" icon={icon} aria-label={title} {...buttonProps} />
    </Tooltip>
  );
}
