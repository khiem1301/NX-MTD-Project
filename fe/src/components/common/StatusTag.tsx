import { Tag } from 'antd';

export type StatusLabel = { text: string; color: string };

type StatusTagProps = {
  status: string;
  labels: Readonly<Record<string, StatusLabel>>;
};

/** Hiển thị trạng thái BE (VD "PENDING") bằng nhãn tiếng Việt + màu. */
export function StatusTag({ status, labels }: StatusTagProps) {
  const label = labels[status];
  return <Tag color={label?.color ?? 'default'}>{label?.text ?? status}</Tag>;
}
