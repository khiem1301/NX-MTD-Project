import { Button, Popover, Tooltip, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { fill, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import type { KpiTargetLine } from '../types';

const text = KPI_TEXT.table;
const form = KPI_TEXT.form;

/** Ô "Màu × size" của bảng KPI: số dòng, bấm xem chỉ tiêu và QC đạt của từng màu × size. Chỉ tiêu cũ chỉ có số tổng. */
export function KpiLinesCell({ lines }: { lines: KpiTargetLine[] | null }) {
  if (!lines || lines.length === 0) {
    return (
      <Tooltip title={text.noLinesHint}>
        <Typography.Text type="secondary">{text.noLines}</Typography.Text>
      </Tooltip>
    );
  }
  const content = (
    <DataTable<KpiTargetLine>
      rowKey="orderLineId"
      size="small"
      pagination={false}
      dataSource={lines}
      scroll={{ y: 320 }}
      columns={[
        { title: form.lineColor, dataIndex: 'color' },
        { title: form.lineSize, dataIndex: 'sizeCode', width: 80 },
        {
          title: KPI_TEXT.table.progress,
          key: 'qty',
          align: 'right',
          width: 160,
          render: (_, l) => fill(text.lineValue, formatQty(l.actualQty), formatQty(l.targetQty)),
        },
      ]}
    />
  );
  return (
    <Popover content={content} title={text.lines} trigger="click" styles={{ body: { width: 380 } }}>
      <Button type="link" size="small" style={{ padding: 0 }}>
        {fill(text.lineCount, lines.length)}
      </Button>
    </Popover>
  );
}
