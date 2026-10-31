import { Descriptions, type TableColumnsType } from 'antd';
import { DetailDescriptions, DetailModal } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import { LABELS, LAY_STATUS_LABEL, SHIFT_LABEL } from '../constants';
import type { CuttingLay, CuttingLayLine } from '../types';
import { DataTable } from '@/components/common/DataTable';

const columns: TableColumnsType<CuttingLayLine> = [
  { title: LABELS.orderLine, key: 'line', render: (_, l) => `${l.color} - ${l.sizeCode}` },
  { title: LABELS.calculatedQty, dataIndex: 'calculatedQty', align: 'right' },
  { title: LABELS.actualQty, dataIndex: 'actualQty', align: 'right' },
  {
    title: MESSAGES.common.reason,
    dataIndex: 'adjustReason',
    render: (v: string | null) => v ?? '—',
  },
];

/** Chi tiết một bàn cắt: thông tin chung và số cắt từng màu - size (kèm lý do khi số thực tế khác số tính). */
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
            <Descriptions.Item label={LABELS.fabricColor}>
              {lay.fabricColor || '—'}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.plyCount}>{lay.plyCount}</Descriptions.Item>
            <Descriptions.Item label={LABELS.sizeRatio}>{lay.sizeRatio}</Descriptions.Item>
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
