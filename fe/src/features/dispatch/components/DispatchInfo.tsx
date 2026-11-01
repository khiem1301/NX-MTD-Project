import { Descriptions } from 'antd';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { formatDate, formatDateTime } from '@/utils/format';
import { DISPATCH_LABELS, DISPATCH_STATUS_LABEL } from '../constants';
import type { DispatchNote } from '../types';

/** Thông tin chung của phiếu xuất giao: đơn, khách, ngày giao, trạng thái, địa chỉ, người xuất, ghi chú. */
export function DispatchInfo({ note }: { note: DispatchNote }) {
  return (
    <DetailDescriptions>
      <Descriptions.Item label={DISPATCH_LABELS.order}>{note.orderCode}</Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.customer}>{note.customerName}</Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.deliveryDate}>
        {formatDate(note.deliveryDate)}
      </Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.status}>
        <StatusTag status={note.status} labels={DISPATCH_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.deliveryAddress} span={2}>
        {note.deliveryAddress}
      </Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.dispatchedBy}>
        {note.dispatchedByName
          ? `${note.dispatchedByName} · ${formatDateTime(note.dispatchedAt)}`
          : '—'}
      </Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.customerReceivedDate}>
        {note.customerReceivedDate ? formatDate(note.customerReceivedDate) : '—'}
      </Descriptions.Item>
      <Descriptions.Item label={DISPATCH_LABELS.note} span={2}>
        {note.note ?? '—'}
      </Descriptions.Item>
    </DetailDescriptions>
  );
}
