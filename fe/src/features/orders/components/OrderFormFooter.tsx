import { Alert, Button, Space, Typography, theme } from 'antd';
import type { CSSProperties } from 'react';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty } from '@/utils/format';

type OrderFormFooterProps = {
  saveError: string | undefined;
  totals: { qty: number; amount: number };
  editable: boolean;
  saving: boolean;
  onCancel: () => void;
  onSave: () => void;
};

/** Thanh tổng và nút Lưu dính đáy màn hình: đơn nhiều mã hàng không phải cuộn xuống cuối để lưu. */
export function OrderFormFooter({
  saveError,
  totals,
  editable,
  saving,
  onCancel,
  onSave,
}: OrderFormFooterProps) {
  const { token } = theme.useToken();
  const style: CSSProperties = {
    position: 'sticky',
    bottom: 0,
    zIndex: 10,
    display: 'flex',
    flexWrap: 'wrap',
    gap: 8,
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 12,
    padding: '12px 0',
    background: token.colorBgContainer,
    borderTop: `1px solid ${token.colorBorderSecondary}`,
  };
  return (
    <div style={style}>
      {saveError && <Alert type="error" showIcon message={saveError} style={{ width: '100%' }} />}
      <Typography.Text strong>
        {`${MESSAGES.orders.totalQty}: ${formatQty(totals.qty)} · ${MESSAGES.orders.totalAmount}: ${formatCurrency(totals.amount)}`}
      </Typography.Text>
      <Space>
        <Button onClick={onCancel}>{MESSAGES.common.cancel}</Button>
        <Button type="primary" disabled={!editable} loading={saving} onClick={onSave}>
          {MESSAGES.common.save}
        </Button>
      </Space>
    </div>
  );
}
