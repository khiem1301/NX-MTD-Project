import { Descriptions, type TableColumnsType } from 'antd';
import { DetailDescriptions, DetailModal } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { formatDate, formatQty } from '@/utils/format';
import { LABELS, LAY_STATUS_LABEL, SHIFT_LABEL } from '../constants';
import type { CuttingLay, CuttingLayLine } from '../types';
import { formatLayFabrics } from '../utils/layRemaining';
import { DataTable } from '@/components/common/DataTable';

const columns: TableColumnsType<CuttingLayLine> = [
  {
    title: LABELS.orderLine,
    key: 'line',
    render: (_, l) => `${l.color} - ${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`,
  },
  { title: LABELS.actualQty, dataIndex: 'actualQty', align: 'right' },
];

/** Chi tiết một bàn cắt: thông tin chung, số mét vải đã dùng và số lượng sau cắt từng màu - size (DEV-ALL-90). */
export function LayDetailModal({ lay, onClose }: { lay: CuttingLay | null; onClose: () => void }) {
  return (
    <DetailModal
      open={lay !== null}
      onClose={onClose}
      title={lay ? `${LABELS.laySequence} ${lay.laySequence}` : undefined}
    >
      {lay && (
        <>
          <DetailDescriptions>
            <Descriptions.Item label={LABELS.workDate}>
              {formatDate(lay.workDate)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.shift}>
              {SHIFT_LABEL[lay.shiftCode] ?? lay.shiftCode}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.layFabrics} span={2}>
              {formatLayFabrics(lay.fabrics, formatQty)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.sizeSummary}>{lay.sizeSummary}</Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={lay.status} labels={LAY_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.note}>{lay.note ?? '—'}</Descriptions.Item>
          </DetailDescriptions>
          <DataTable<CuttingLayLine>
            rowKey="cuttingLayLineId"
            size="small"
            style={{ marginTop: 16 }}
            columns={columns}
            dataSource={lay.lines}
            pagination={false}
          />
        </>
      )}
    </DetailModal>
  );
}
