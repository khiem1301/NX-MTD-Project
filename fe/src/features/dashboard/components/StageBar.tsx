import { Progress, Tooltip } from 'antd';

const pct = (value: number, total: number) =>
  total > 0 ? Math.min(100, Math.round((100 * value) / total)) : 0;

export function StageBar({ value, total, label }: { value: number; total: number; label: string }) {
  return (
    <Tooltip title={`${label}: ${value} / ${total}`}>
      <Progress
        percent={pct(value, total)}
        size="small"
        format={() => value}
        style={{ minWidth: 90, margin: 0 }}
      />
    </Tooltip>
  );
}
