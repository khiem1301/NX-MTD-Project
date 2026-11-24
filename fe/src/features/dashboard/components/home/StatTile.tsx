import type { ReactNode } from 'react';
import { Card, Typography, theme } from 'antd';
import { useNavigate } from 'react-router-dom';

export type StatTone = 'default' | 'danger' | 'warn' | 'primary' | 'ok';

type StatTileProps = {
  label: ReactNode;
  value: ReactNode;
  /** Dòng phụ dưới số (vd "đang SX / giao dở"). */
  note?: ReactNode;
  tone?: StatTone;
  /** Có đường dẫn thì ô bấm được và chuyển tới màn nghiệp vụ tương ứng. */
  to?: string;
};

/** Ô số liệu của trang chủ: nhãn, số lớn có màu theo mức độ, dòng phụ. */
export function StatTile({ label, value, note, tone = 'default', to }: StatTileProps) {
  const { token } = theme.useToken();
  const navigate = useNavigate();
  const color: Record<StatTone, string | undefined> = {
    default: undefined,
    danger: token.colorError,
    warn: token.colorWarning,
    primary: token.colorPrimary,
    ok: token.colorSuccess,
  };
  return (
    <Card
      size="small"
      hoverable={to !== undefined}
      onClick={to ? () => navigate(to) : undefined}
      style={{ height: '100%', cursor: to ? 'pointer' : 'default' }}
    >
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {label}
      </Typography.Text>
      <div style={{ fontSize: 24, fontWeight: 700, lineHeight: 1.15, color: color[tone] }}>
        {value}
      </div>
      {note && <div style={{ marginTop: 4 }}>{note}</div>}
    </Card>
  );
}
