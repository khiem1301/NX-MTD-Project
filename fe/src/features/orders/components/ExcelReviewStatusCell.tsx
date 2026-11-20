import { Tag, Typography } from 'antd';
import { ReasonHover } from '@/components/common/ReasonHover';
import { MESSAGES } from '@/constants/messages';
import { EXCEL_ROW_ACTION } from '../constants';
import type { RowStatus } from '../utils/excelReview';

const text = MESSAGES.orders.excelReview;

type Look = { color: string; label: string; type?: 'danger' | 'secondary' | 'warning' };

/** Nhãn theo kết quả kiểm; Mới / Cập nhật / Không đổi đều là "Hợp lệ" và không cần lý do. */
const LOOK: Record<string, Look> = {
  [EXCEL_ROW_ACTION.ERROR]: { color: 'red', label: text.status.error, type: 'danger' },
  [EXCEL_ROW_ACTION.PENDING]: { color: 'default', label: text.status.pending, type: 'secondary' },
  [EXCEL_ROW_ACTION.EXISTS]: { color: 'orange', label: text.status.exists, type: 'warning' },
  [EXCEL_ROW_ACTION.NEW_MATERIAL]: {
    color: 'orange',
    label: text.status.newMaterial,
    type: 'warning',
  },
};
const VALID: Look = { color: 'green', label: text.status.valid };

type ExcelReviewStatusCellProps = {
  status: RowStatus | undefined;
  parseError?: string | null;
  /** Đang Thêm mới: mã hàng đã có thì nhắc đổi mã (Thêm mới sẽ báo trùng). */
  creating?: boolean;
};

/** DEV-ALL-122/123: ô kết quả của popup kiểm tra Excel — Hợp lệ / Lỗi / Chờ kiểm / Đã có / NVL chưa có, kèm lý do. */
export function ExcelReviewStatusCell({
  status,
  parseError,
  creating,
}: ExcelReviewStatusCellProps) {
  if (!status)
    return parseError ? (
      <ReasonHover reasons={[{ text: parseError, type: 'danger' }]}>
        <Tag color="red">{text.status.error}</Tag>
      </ReasonHover>
    ) : (
      <Typography.Text type="secondary">{text.status.checking}</Typography.Text>
    );
  const look = LOOK[status.action] ?? VALID;
  const reason =
    status.action === EXCEL_ROW_ACTION.EXISTS && creating
      ? text.existsCreate
      : look === VALID
        ? null
        : status.error;
  return (
    <ReasonHover reasons={[reason && { text: reason, type: look.type ?? 'secondary' }]}>
      <Tag color={look.color}>{look.label}</Tag>
    </ReasonHover>
  );
}
