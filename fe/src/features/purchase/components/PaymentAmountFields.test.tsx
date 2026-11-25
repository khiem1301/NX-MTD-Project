import { render, screen } from '@testing-library/react';
import { Form } from 'antd';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';
import { PaymentAmountFields } from './PaymentAmountFields';

const text = PURCHASE_TEXT.pay;

const order = {
  totalAmount: 1_000_000,
  receivedAmount: 800_000,
  actualAmount: null,
  actualAmountSetAt: null,
} as PurchaseOrderDetail;

function Harness({
  closedShort,
  paid,
  actual,
}: {
  closedShort: boolean;
  paid: number;
  /** Giá trị thực tế đã nhập ở nút riêng; bỏ trống = chưa nhập. */
  actual?: number;
}) {
  const [form] = Form.useForm();
  const entered =
    actual === undefined
      ? order
      : { ...order, actualAmount: actual, actualAmountSetAt: '2026-11-25T08:00:00' };
  return (
    <Form form={form} initialValues={{ paidAmount: paid }}>
      <PaymentAmountFields order={entered} form={form} closedShort={closedShort} />
    </Form>
  );
}

describe('PaymentAmountFields', () => {
  it('không còn ô nhập giá trị thực tế trong form thanh toán (nhập ở nút riêng)', () => {
    render(<Harness closedShort paid={800_000} actual={800_000} />);
    expect(screen.queryByText(text.actualAmount)).not.toBeInTheDocument();
  });

  it('Chốt không bù chưa nhập giá trị thực tế: báo phải nhập trước', () => {
    render(<Harness closedShort paid={500_000} />);
    expect(screen.getByText(text.actualMissing)).toBeInTheDocument();
  });

  it('Chốt không bù: trả đủ thực tế báo Đã thanh toán', async () => {
    render(<Harness closedShort paid={800_000} actual={800_000} />);
    expect(await screen.findByText(text.willBePaid)).toBeInTheDocument();
  });

  it('Chốt không bù: trả thiếu báo Thanh toán một phần', async () => {
    render(<Harness closedShort paid={500_000} actual={800_000} />);
    expect(await screen.findByText(text.willBePartial)).toBeInTheDocument();
  });

  it('đơn thường: trả thiếu báo khác giá trị đơn', async () => {
    render(<Harness closedShort={false} paid={500_000} />);
    expect(await screen.findByText(text.differsFromTotal)).toBeInTheDocument();
  });
});
