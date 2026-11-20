import type { ReactNode } from 'react';
import { InfoCircleOutlined } from '@ant-design/icons';
import { Space, Tooltip } from 'antd';

export type Reason = { text: string; type: 'danger' | 'warning' | 'secondary' };

const COLOR: Record<Reason['type'], string> = {
  danger: 'red',
  warning: 'orange',
  secondary: 'gray',
};
/** Mức nặng nhất quyết định màu tooltip và biểu tượng. */
const SEVERITY: Reason['type'][] = ['danger', 'warning', 'secondary'];

type ReasonHoverProps = {
  reasons: readonly (Reason | null | undefined | false | '')[];
  children: ReactNode;
};

/**
 * Nhãn kết quả của bảng xem trước khi nhập Excel: lỗi / cảnh báo / ghi chú không hiện sẵn dưới nhãn mà hiện khi trỏ chuột vào
 * (có biểu tượng ⓘ báo dòng đó có lý do). Không có lý do nào thì chỉ hiện nhãn.
 */
export function ReasonHover({ reasons, children }: ReasonHoverProps) {
  const list = reasons.filter((r): r is Reason => Boolean(r));
  if (list.length === 0) return <>{children}</>;
  const worst = SEVERITY.find((t) => list.some((r) => r.type === t)) ?? 'secondary';
  return (
    <Tooltip
      color={COLOR[worst]}
      title={list.map((r) => (
        <div key={r.text}>{r.text}</div>
      ))}
    >
      <Space size={4} style={{ cursor: 'help' }}>
        {children}
        <InfoCircleOutlined style={{ color: COLOR[worst] }} />
      </Space>
    </Tooltip>
  );
}
