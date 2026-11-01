import { ArrowLeftOutlined } from '@ant-design/icons';
import { Button, Tooltip } from 'antd';

type BackButtonProps = {
  onClick: () => void;
  disabled?: boolean;
};

/** Nút icon "quay lại trang trước", đặt ở góc trái trên cùng của màn xem chi tiết / thêm / sửa. */
export function BackButton({ onClick, disabled }: BackButtonProps) {
  return (
    <Tooltip title="Quay lại">
      <Button
        type="text"
        shape="circle"
        aria-label="Quay lại"
        icon={<ArrowLeftOutlined />}
        disabled={disabled}
        onClick={onClick}
      />
    </Tooltip>
  );
}
