import { Tooltip } from 'antd';

type MaterialCodeTipProps = {
  code: string;
  name: string;
  color?: string | null;
};

/** Chỉ hiện mã NVL cho gọn; trỏ vào thì hiện tên (kèm màu). */
export function MaterialCodeTip({ code, name, color }: MaterialCodeTipProps) {
  return (
    <Tooltip title={color ? `${name} - ${color}` : name}>
      <b style={{ cursor: 'help' }}>{code}</b>
    </Tooltip>
  );
}
