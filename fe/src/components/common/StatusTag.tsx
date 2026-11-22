import { Tag, Tooltip } from 'antd';

export type StatusLabel = { text: string; color: string };

type StatusTagProps = {
  status: string;
  labels: Readonly<Record<string, StatusLabel>>;
  /** Tooltip khi rê chuột (VD người xác nhận, lý do ngừng). */
  title?: string;
};

/** Hiển thị trạng thái BE (VD "PENDING") bằng nhãn tiếng Việt + màu. */
export function StatusTag({ status, labels, title }: StatusTagProps) {
  const label = labels[status];
  const tag = <Tag color={label?.color ?? 'default'}>{label?.text ?? status}</Tag>;
  return title ? <Tooltip title={title}>{tag}</Tooltip> : tag;
}
