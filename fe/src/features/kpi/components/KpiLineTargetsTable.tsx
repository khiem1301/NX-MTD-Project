import { Alert, Form, Tooltip, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { fill, formatQty } from '@/utils/format';
import { QtyInput } from '@/components/common/QtyInput';
import { KPI_TEXT } from '../constants';
import type { KpiLineCapacity } from '../types';
import { KPI_MAX_QTY } from '../utils/periodRule';
import { lineQtyError, type LineQtyValues } from '../utils/lineTargets';

const text = KPI_TEXT.form;

/** Mã hàng nhiều màu × size thì bảng cuộn bên trong, nút Lưu vẫn nằm trong màn hình. */
const TABLE_BODY_HEIGHT = 'max(160px, calc(100vh - 640px))';

type Props = {
  lines: KpiLineCapacity[];
  values: LineQtyValues | undefined;
  loading: boolean;
  /** Chưa chọn tổ thì chỉ hiện gợi ý. */
  hasTeam: boolean;
  total: number;
  /** Chỉ tiêu cũ chỉ có số tổng (đang sửa): báo phải nhập lại theo màu × size. */
  legacyTotal: number | null;
};

/** Bảng nhập theo màu × size kèm tổng cả kỳ và lời nhắc chỉ tiêu cũ. */
export function KpiLineTargetsTable({ total, legacyTotal, ...table }: Props) {
  const showTotal = table.hasTeam && table.lines.length > 0;
  return (
    <>
      {legacyTotal !== null && (
        <Alert
          type="info"
          showIcon
          message={fill(text.linesLegacy, formatQty(legacyTotal))}
          style={{ marginBottom: 8 }}
        />
      )}
      <LinesTable {...table} />
      {showTotal && (
        <Typography.Text style={{ display: 'block', marginTop: 8 }}>
          {text.linesTotal}: <strong>{formatQty(total)}</strong>
          {total === 0 && (
            <Typography.Text type="secondary"> · {text.linesRequired}</Typography.Text>
          )}
        </Typography.Text>
      )}
    </>
  );
}

/** Nhập chỉ tiêu cho từng màu × size tổ được chia; giá trị nằm trong form ở trường lineQty, khóa là orderLineId. */
function LinesTable({ lines, values, loading, hasTeam }: Omit<Props, 'total' | 'legacyTotal'>) {
  if (!hasTeam || (!loading && lines.length === 0)) {
    return (
      <Typography.Text
        type="secondary"
        style={{ display: 'block', padding: '8px 0', textAlign: 'center' }}
      >
        {hasTeam ? text.linesEmpty : text.linesPickTeam}
      </Typography.Text>
    );
  }
  return (
    <DataTable<KpiLineCapacity>
      rowKey="orderLineId"
      size="small"
      pagination={false}
      loading={loading}
      dataSource={lines}
      scroll={{ y: TABLE_BODY_HEIGHT }}
      columns={[
        { title: text.lineColor, dataIndex: 'color' },
        { title: text.lineSize, dataIndex: 'sizeCode', width: 80 },
        {
          title: text.lineOrdered,
          dataIndex: 'orderedQty',
          align: 'right',
          width: 90,
          render: formatQty,
        },
        {
          title: text.lineAssigned,
          dataIndex: 'assignedToOthers',
          align: 'right',
          width: 110,
          render: formatQty,
        },
        {
          title: text.lineMax,
          dataIndex: 'maxTarget',
          align: 'right',
          width: 90,
          render: (qty: number) => <strong>{formatQty(qty)}</strong>,
        },
        // Chỉ khi sửa chỉ tiêu đã có QC đạt: không hạ chỉ tiêu dưới số này.
        ...(lines.some((l) => l.actualQty > 0)
          ? [
              {
                title: text.lineActual,
                dataIndex: 'actualQty',
                align: 'right' as const,
                width: 80,
                render: formatQty,
              },
            ]
          : []),
        {
          title: text.lineTarget,
          key: 'target',
          width: 140,
          render: (_, l) => {
            const error = lineQtyError(l, values?.[String(l.orderLineId)]);
            return (
              <Tooltip title={error}>
                <Form.Item name={['lineQty', String(l.orderLineId)]} noStyle>
                  <QtyInput
                    size="small"
                    min={0}
                    max={KPI_MAX_QTY}
                    status={error ? 'error' : undefined}
                    aria-label={fill(text.lineInput, l.color, l.sizeCode)}
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              </Tooltip>
            );
          },
        },
      ]}
    />
  );
}
