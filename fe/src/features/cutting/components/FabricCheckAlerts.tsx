import { Alert, Flex } from 'antd';
import { LABELS } from '../constants';
import type { WorkOrderFabricCheck } from '../types';
import { fabricPlanLine } from '../utils/fabricPlan';

type Check = WorkOrderFabricCheck;

const Note = ({ text }: { text: string }) => (
  <div style={{ fontSize: 12, opacity: 0.65 }}>{text}</div>
);

const Group = ({
  type,
  title,
  lines,
  note,
}: {
  type: 'warning' | 'info';
  title: string;
  lines: { key: number; text: string }[];
  note?: string;
}) =>
  lines.length === 0 ? null : (
    <Alert
      type={type}
      showIcon
      message={
        <>
          <strong>{title}</strong>
          {lines.map((l) => (
            <div key={l.key}>{l.text}</div>
          ))}
          {note && <Note text={note} />}
        </>
      }
    />
  );

/**
 * Vải của lệnh cắt ước theo bìa của đơn (DEV-ALL-85, ghép bìa DEV-ALL-88): luôn hiện số mét cần và bìa nên dùng (kèm tỉ lệ size của bìa)
 * do hệ thống tính (ước tính, có thể chênh lệch với thực tế); thiếu vải, vải không đủ rộng cho bìa (DEV-ALL-103), chưa tính được thì
 * cảnh báo riêng. Chỉ hiển thị, không chặn lập lệnh.
 */
export function FabricCheckAlerts({ checks }: { checks: Check[] }) {
  const linesOf = (status: Check['status']) =>
    checks
      .filter((c) => c.status === status)
      .map((c) => ({ key: c.materialId, text: c.message ?? '' }));
  const narrow = checks.flatMap((c) =>
    c.widthWarning ? [{ key: c.materialId, text: c.widthWarning }] : [],
  );
  const plans = checks.flatMap((c) => {
    const text = fabricPlanLine(c);
    return text === null ? [] : [{ key: c.materialId, text }];
  });
  if (checks.length === 0) return null;
  return (
    <Flex vertical gap={8} style={{ marginTop: 12 }}>
      <Group type="info" title={LABELS.fabricPlan} lines={plans} note={LABELS.fabricPlanNote} />
      <Group
        type="warning"
        title={LABELS.fabricNotEnough}
        lines={linesOf('Short')}
        note={LABELS.fabricEstimateNote}
      />
      <Group
        type="warning"
        title={LABELS.fabricTooNarrow}
        lines={narrow}
        note={LABELS.fabricEstimateNote}
      />
      <Group type="info" title={LABELS.fabricNotEstimated} lines={linesOf('Unknown')} />
    </Flex>
  );
}
