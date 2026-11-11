import { Descriptions } from 'antd';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { fill, formatDateTime } from '@/utils/format';
import { formatDeliveryWindow } from '@/utils/expectedDelivery';
import { LABELS, SHORTAGE_RESOLUTION_LABEL, SHORTAGE_STATUS_LABEL } from '../constants';
import type { ShortDelivery } from '../types';

/** Thông tin chung của yêu cầu giao thiếu NVL: phiếu nhập, trạng thái, đơn, hướng xử lý, ngày gửi, ghi chú kết quả. */
export function ShortDeliveryInfo({ detail: d }: { detail: ShortDelivery }) {
  return (
    <DetailDescriptions style={{ marginBottom: 16 }}>
      <Descriptions.Item label={LABELS.receiptCode}>{d.receiptCode}</Descriptions.Item>
      <Descriptions.Item label={LABELS.status}>
        <StatusTag status={d.status} labels={SHORTAGE_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.order}>{d.orderCode ?? '—'}</Descriptions.Item>
      <Descriptions.Item label={LABELS.purchaseOrder}>{d.poCode ?? '—'}</Descriptions.Item>
      <Descriptions.Item label={LABELS.shortageResolution}>
        {d.resolution ? (
          <StatusTag status={d.resolution} labels={SHORTAGE_RESOLUTION_LABEL} />
        ) : (
          '—'
        )}
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.submittedAt}>
        {formatDateTime(d.createdAt)}
      </Descriptions.Item>
      {/* Số lần giao bù luôn >= 1 khi đã chọn giao bù. */}
      {!!d.expectedDeliveryCount && (
        <Descriptions.Item label={LABELS.replenishPlan} span={2}>
          {fill(
            LABELS.replenishPlanValue,
            d.replenishDeliveryCount ?? 0,
            d.expectedDeliveryCount,
            formatDeliveryWindow(d.expectedDeliveryDate, d.expectedDeliveryDateTo),
          )}
        </Descriptions.Item>
      )}
      {d.resultNote && (
        <Descriptions.Item label={LABELS.note} span={2}>
          {d.resultNote}
        </Descriptions.Item>
      )}
    </DetailDescriptions>
  );
}
