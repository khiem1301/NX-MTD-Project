import { Alert } from 'antd';
import { MESSAGES } from '@/constants/messages';

const text = MESSAGES.orders.orderExcel;

type OrderMissingDataAlertProps = {
  missingMarkers: boolean;
  missingMaterials: boolean;
  /** Gợi ý cách bổ sung (form đơn / chi tiết đơn). */
  hint: string;
};

/** DEV-ALL-150: cảnh báo đơn chưa có bìa cắt của đơn hoặc NVL của đơn; đủ cả hai thì không hiện. */
export function OrderMissingDataAlert({
  missingMarkers,
  missingMaterials,
  hint,
}: OrderMissingDataAlertProps) {
  if (!missingMarkers && !missingMaterials) return null;
  const parts = [
    missingMarkers ? text.missingMarkers : null,
    missingMaterials ? text.missingMaterials : null,
  ].filter(Boolean);
  return (
    <Alert
      type="warning"
      showIcon
      style={{ margin: '8px 0' }}
      message={text.missingTitle}
      description={`${parts.join(' ')} ${hint}`}
    />
  );
}
