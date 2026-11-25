import { Alert, Form, type FormInstance } from 'antd';
import { MoneyInput } from '@/components/common/MoneyInput';
import { formatCurrency } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';

const text = PURCHASE_TEXT.pay;

type Props = { order: PurchaseOrderDetail; form: FormInstance; closedShort: boolean };

const isPositive = (value: unknown): value is number => typeof value === 'number' && value > 0;

/**
 * Ô số tiền thanh toán. Đơn Chốt không bù xét theo giá trị thực tế đã nhập ở nút riêng (DEV-ALL-129, 185):
 * trả đủ → Đã thanh toán, trả thiếu → Thanh toán một phần; chưa nhập giá trị thực tế thì chưa thanh toán được.
 */
export function PaymentAmountFields({ order, form, closedShort }: Props) {
  const paid = Form.useWatch('paidAmount', form);
  const actual = order.actualAmountSetAt === null ? null : order.actualAmount;
  const alreadyPaid = order.paidAmount ?? 0;
  const target = closedShort ? actual : order.totalAmount;
  const exceedsMessage = closedShort ? text.amountExceedsActual : text.amountExceedsTotal;
  const showPreview = isPositive(paid) && isPositive(target) && alreadyPaid + paid <= target;
  const fullyPaid = showPreview && Math.round(alreadyPaid + paid) >= Math.round(target);

  return (
    <>
      {closedShort && actual === null && (
        <Alert type="warning" showIcon message={text.actualMissing} style={{ marginBottom: 16 }} />
      )}
      <Form.Item
        name="paidAmount"
        label={text.amount}
        validateTrigger="onSubmit"
        rules={[
          { required: true, type: 'number', min: 0.01, message: text.amountRequired },
          {
            validator: async (_, value) => {
              const max = closedShort ? actual : order.totalAmount;
              if (
                typeof value !== 'number' ||
                typeof max !== 'number' ||
                value <= max - alreadyPaid
              )
                return;
              throw new Error(exceedsMessage);
            },
          },
        ]}
        extra={
          closedShort
            ? undefined
            : `${text.remaining}: ${formatCurrency(Math.max(0, order.totalAmount - alreadyPaid))} (${PURCHASE_TEXT.detail.total}: ${formatCurrency(order.totalAmount)})`
        }
      >
        <MoneyInput style={{ width: 240 }} />
      </Form.Item>
      {showPreview && (closedShort || !fullyPaid) && (
        <Alert
          type={fullyPaid ? 'success' : 'warning'}
          showIcon
          message={
            closedShort ? (fullyPaid ? text.willBePaid : text.willBePartial) : text.differsFromTotal
          }
          style={{ marginBottom: 16 }}
        />
      )}
    </>
  );
}
