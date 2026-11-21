import { Alert, Form, InputNumber, type FormInstance } from 'antd';
import { fill, formatCurrency } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';

const text = PURCHASE_TEXT.pay;

type Props = { order: PurchaseOrderDetail; form: FormInstance; closedShort: boolean };

const isPositive = (value: unknown): value is number => typeof value === 'number' && value > 0;

/**
 * Ô số tiền thanh toán. Đơn Chốt không bù có thêm ô Tổng giá trị thực tế (DEV-ALL-129):
 * trạng thái thanh toán xét theo giá trị này — trả đủ → Đã thanh toán, trả thiếu → Thanh toán một phần.
 */
export function PaymentAmountFields({ order, form, closedShort }: Props) {
  const paid = Form.useWatch('paidAmount', form);
  const actual = Form.useWatch('actualAmount', form);
  const alreadyPaid = order.paidAmount ?? 0;
  const target = closedShort ? actual : order.totalAmount;
  const exceedsMessage = closedShort ? text.amountExceedsActual : text.amountExceedsTotal;
  const showPreview = isPositive(paid) && isPositive(target) && alreadyPaid + paid <= target;
  const fullyPaid = showPreview && Math.round(alreadyPaid + paid) >= Math.round(target);

  return (
    <>
      {closedShort && (
        <Form.Item
          name="actualAmount"
          label={text.actualAmount}
          validateTrigger="onSubmit"
          rules={[
            { required: true, type: 'number', min: 0.01, message: text.actualAmountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number' || value <= order.totalAmount) return;
                throw new Error(text.actualExceedsTotal);
              },
            },
          ]}
          extra={fill(
            text.actualHint,
            formatCurrency(order.receivedAmount),
            formatCurrency(order.totalAmount),
          )}
        >
          <InputNumber
            min={0}
            precision={0}
            addonAfter="₫"
            style={{ width: 240 }}
            onChange={(value) =>
              form.setFieldValue(
                'paidAmount',
                typeof value === 'number' && value > alreadyPaid ? value - alreadyPaid : null,
              )
            }
          />
        </Form.Item>
      )}
      <Form.Item
        name="paidAmount"
        label={text.amount}
        validateTrigger="onSubmit"
        rules={[
          { required: true, type: 'number', min: 0.01, message: text.amountRequired },
          {
            validator: async (_, value) => {
              const max = closedShort ? form.getFieldValue('actualAmount') : order.totalAmount;
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
        <InputNumber min={0} precision={0} addonAfter="₫" style={{ width: 240 }} />
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
