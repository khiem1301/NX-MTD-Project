import { Descriptions } from 'antd';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { formatDate, formatDateTime } from '@/utils/format';
import { LABELS, RECEIPT_STATUS_LABEL, RECEIPT_TYPE_LABEL, SOURCE_LABEL } from '../constants';
import type { MaterialReceipt } from '../types';

/** Thông tin chung của phiếu nhập: loại, nguồn, đơn, đơn mua, trạng thái, ngày, người xác nhận, ghi chú. */
export function ReceiptInfo({ receipt: r }: { receipt: MaterialReceipt }) {
  return (
    <DetailDescriptions>
      <Descriptions.Item label={LABELS.receiptType}>
        {RECEIPT_TYPE_LABEL[r.receiptType] ?? r.receiptType}
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.receiptSource}>
        {SOURCE_LABEL[r.source] ?? r.source}
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.order}>{r.orderCode ?? '—'}</Descriptions.Item>
      <Descriptions.Item label={LABELS.purchaseOrder}>{r.poCode ?? '—'}</Descriptions.Item>
      {r.originalReceiptCode && (
        <Descriptions.Item label={LABELS.originalReceipt}>
          {r.originalReceiptCode}
        </Descriptions.Item>
      )}
      <Descriptions.Item label={LABELS.status}>
        <StatusTag status={r.status} labels={RECEIPT_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.receiptDate}>{formatDate(r.receiptDate)}</Descriptions.Item>
      <Descriptions.Item label={LABELS.submittedAt}>
        {formatDateTime(r.submittedAt)}
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.confirmedBy}>
        {r.confirmedByName ? `${r.confirmedByName} · ${formatDateTime(r.confirmedAt)}` : '—'}
      </Descriptions.Item>
      <Descriptions.Item label={LABELS.note}>{r.note ?? '—'}</Descriptions.Item>
    </DetailDescriptions>
  );
}
