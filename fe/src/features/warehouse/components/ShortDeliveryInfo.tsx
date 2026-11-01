import { Descriptions } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { formatDateTime } from '@/utils/format';
import { LABELS, SHORTAGE_RESOLUTION_LABEL, SHORTAGE_STATUS_LABEL } from '../constants';
import type { ShortDelivery } from '../types';

/** Thông tin chung của yêu cầu giao thiếu NVL: phiếu nhập, trạng thái, đơn, hướng xử lý, ngày gửi, ghi chú kết quả. */
export function ShortDeliveryInfo({ detail: d }: { detail: ShortDelivery }) {
  return (
    <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
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
      {d.resultNote && (
        <Descriptions.Item label={LABELS.note} span={2}>
          {d.resultNote}
        </Descriptions.Item>
      )}
    </Descriptions>
  );
}
