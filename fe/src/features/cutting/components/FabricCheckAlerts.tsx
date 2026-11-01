import { Alert, Flex } from 'antd';
import { LABELS } from '../constants';
import type { WorkOrderFabricCheck } from '../types';

type Check = WorkOrderFabricCheck;

const Group = ({
  type,
  title,
  rows,
}: {
  type: 'warning' | 'info';
  title: string;
  rows: Check[];
}) =>
  rows.length === 0 ? null : (
    <Alert
      type={type}
      showIcon
      message={
        <>
          <strong>{title}</strong>
          {rows.map((r) => (
            <div key={r.materialId}>{r.message}</div>
          ))}
          {type === 'warning' && (
            <div style={{ fontSize: 12, opacity: 0.65 }}>{LABELS.fabricEstimateNote}</div>
          )}
        </>
      }
    />
  );

/** Cảnh báo vải của lệnh cắt theo kích thước chi tiết: chỉ hiển thị, không chặn lập lệnh. Đủ vải thì không hiện gì. */
export function FabricCheckAlerts({ checks }: { checks: Check[] }) {
  const of = (status: Check['status']) => checks.filter((c) => c.status === status);
  if (checks.every((c) => c.status === 'Enough')) return null;
  return (
    <Flex vertical gap={8} style={{ marginTop: 12 }}>
      <Group type="warning" title={LABELS.fabricNotEnough} rows={of('Short')} />
      <Group type="warning" title={LABELS.fabricTooWide} rows={of('TooWide')} />
      <Group type="info" title={LABELS.fabricNotEstimated} rows={of('Unknown')} />
    </Flex>
  );
}
