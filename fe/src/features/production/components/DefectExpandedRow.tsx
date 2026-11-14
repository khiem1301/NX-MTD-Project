import { Descriptions } from 'antd';
import { formatCurrency, formatDateTime } from '@/utils/format';
import { REQUEST_STATUS_LABEL, type RequestStatus } from '@/constants/requestTypes';
import { PRODUCTION_TEXT } from '../constants';
import type { Defect } from '../types';

const detail = PRODUCTION_TEXT.defects.detail;

const dash = (v: string | null | undefined) => v || '—';

/**
 * Chi tiết một phiếu hàng hỏng, mở ngay dưới dòng: những thông tin phụ không đủ chỗ ở các cột của bảng. "Cập nhật lần cuối" là
 * giờ sửa hoặc hủy phiếu (người thao tác và ghi chú nằm ở nhật ký thay đổi).
 */
export function DefectExpandedRow({ defect: d }: { defect: Defect }) {
  return (
    <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered>
      <Descriptions.Item label={detail.reasonNote}>{dash(d.reasonNote)}</Descriptions.Item>
      <Descriptions.Item label={detail.rate}>
        {d.scrapUnitRate === null ? '—' : formatCurrency(d.scrapUnitRate)}
      </Descriptions.Item>
      <Descriptions.Item label={detail.recordedAt}>
        {formatDateTime(d.recordedAt)}
      </Descriptions.Item>
      <Descriptions.Item label={detail.deadline}>
        {formatDateTime(d.editDeadline)}
      </Descriptions.Item>
      <Descriptions.Item label={detail.updatedAt}>
        {d.updatedAt ? formatDateTime(d.updatedAt) : '—'}
      </Descriptions.Item>
      <Descriptions.Item label={detail.replacement}>
        {d.replacementRequestCode
          ? `${d.replacementRequestCode} · ${REQUEST_STATUS_LABEL[d.replacementRequestStatus as RequestStatus] ?? d.replacementRequestStatus}`
          : '—'}
      </Descriptions.Item>
    </Descriptions>
  );
}
