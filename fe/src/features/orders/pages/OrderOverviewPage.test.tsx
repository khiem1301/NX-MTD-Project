import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { MESSAGES } from '@/constants/messages';
import type { OrderOverview } from '../types';
import { OrderOverviewPage } from './OrderOverviewPage';

jest.mock('@/app/orderDetailTabs', () => ({
  ORDER_DETAIL_TABS: [
    {
      key: 'materials',
      label: 'NVL của đơn',
      Component: ({ orderId, isClosed }: { orderId: number; isClosed: boolean }) => (
        <span>{`tab NVL ${orderId} ${isClosed ? 'đã đóng' : 'đang mở'}`}</span>
      ),
    },
  ],
}));

const mockOrder: { data: OrderOverview } = {
  data: {
    orderId: 5,
    orderCode: 'DH2610-001',
    customerName: 'Công ty A',
    receivedDate: '2026-10-01',
    dueDate: '2026-11-01',
    status: 'New',
    materialStatus: 'Awaiting',
    isDelayRisk: false,
    stopReason: null,
    deliveryAddress: '125 Nguyễn Văn Linh, Quận 7',
    note: null,
    createdAt: '2026-10-01T08:00:00',
    lines: [
      {
        orderLineId: 7,
        productId: 1,
        productCode: 'AO-01',
        color: 'Đỏ',
        sizeCode: 'M',
        orderedQty: 10,
      },
    ],
  },
};

jest.mock('../hooks/useOrders', () => ({
  useOrderOverview: () => ({ ...mockOrder, isLoading: false, error: null, refetch: jest.fn() }),
}));
// Tab Bìa cắt có nút nhập Excel (DEV-ALL-115): không gọi API thật trong test.
jest.mock('../hooks/useSpecImport', () => ({
  usePreviewSpecImport: () => ({ mutate: jest.fn(), reset: jest.fn(), isPending: false }),
  useSpecImport: () => ({ mutateAsync: jest.fn(), isPending: false }),
  useProductOrders: () => ({ data: undefined, isLoading: false }),
}));

const renderAt = (url: string) =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/manager/orders/:id" element={<OrderOverviewPage />} />
      </Routes>
    </MemoryRouter>,
  );

describe('OrderOverviewPage', () => {
  beforeEach(() => {
    mockOrder.data = { ...mockOrder.data, status: 'New' };
  });

  it('Quản đốc xem được đơn nhưng không có nút sửa, hủy, dừng, chuyển sản xuất', () => {
    renderAt('/manager/orders/5');

    expect(screen.getByText('DH2610-001')).toBeInTheDocument();
    expect(screen.getByText('AO-01')).toBeInTheDocument();
    expect(screen.getByText('125 Nguyễn Văn Linh, Quận 7')).toBeInTheDocument();
    for (const name of [
      MESSAGES.common.edit,
      MESSAGES.orders.cancelOrder,
      MESSAGES.orders.stopOrder,
      MESSAGES.orders.startProduction,
    ]) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
  });

  it('không hiện đơn giá, thành tiền CMT và tab Lịch sử (GBR-02)', () => {
    renderAt('/manager/orders/5');

    expect(screen.queryByText(MESSAGES.orders.totalAmount)).not.toBeInTheDocument();
    expect(screen.queryByText(new RegExp(MESSAGES.orders.unitPrice))).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: MESSAGES.common.history })).not.toBeInTheDocument();
  });

  it('đường dẫn ?tab=materials mở thẳng tab NVL của đơn', () => {
    renderAt('/manager/orders/5?tab=materials');

    expect(screen.getByRole('tab', { name: 'NVL của đơn', selected: true })).toBeInTheDocument();
    expect(screen.getByText('tab NVL 5 đang mở')).toBeInTheDocument();
  });

  it('đơn đã đóng thì báo tab NVL chỉ đọc', () => {
    mockOrder.data = { ...mockOrder.data, status: 'Cancelled' };

    renderAt('/manager/orders/5?tab=materials');

    expect(screen.getByText('tab NVL 5 đã đóng')).toBeInTheDocument();
  });
});
