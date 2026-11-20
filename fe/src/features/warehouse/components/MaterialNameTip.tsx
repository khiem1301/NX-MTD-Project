import { Tooltip } from 'antd';

type MaterialNameTipProps = {
  name: string;
  color?: string | null;
};

/** Hiện tên NVL cho gọn; trỏ vào thì hiện thêm màu. */
export function MaterialNameTip({ name, color }: MaterialNameTipProps) {
  return (
    <Tooltip title={color ? `${name} - ${color}` : name}>
      <b style={{ cursor: 'help' }}>{name}</b>
    </Tooltip>
  );
}
