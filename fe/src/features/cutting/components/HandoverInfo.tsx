import { Descriptions } from 'antd';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { formatDateTime } from '@/utils/format';
import { HANDOVER_STATUS_LABEL, LABELS } from '../constants';
import type { CutHandover } from '../types';

/** Thông tin chung của phiếu bàn giao: lệnh sản xuất, đơn, tổ cắt, thời điểm giao, trạng thái, người nhận. */
export function HandoverInfo({ handover: h }: { handover: CutHandover }) {
  return (
    <DetailDescriptions>
      <Descriptions.Item label={LABELS.workOrder}>{h.workOrderCode}</Descriptions.Item>
      <Descriptions.Item label={LABELS.order}>{h.orderCode}</Descriptions.Item>
      <Descriptions.Item label={LABELS.cuttingTeam}>{h.cuttingTeamName}</Descriptions.Item>
      <Descriptions.Item label={LABELS.handedAt}>{formatDateTime(h.handedAt)}</Descriptions.Item>
      <Descriptions.Item label={LABELS.status}>
        <StatusTag status={h.status} labels={HANDOVER_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.confirmedBy}>
        {h.confirmedByName ? `${h.confirmedByName} · ${formatDateTime(h.confirmedAt)}` : '—'}
      </Descriptions.Item>
    </DetailDescriptions>
  );
}
