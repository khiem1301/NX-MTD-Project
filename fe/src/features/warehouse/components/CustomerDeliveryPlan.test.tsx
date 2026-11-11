import { render, screen } from '@testing-library/react';
import { LABELS } from '../constants';
import type { OrderCustomerDelivery } from '../types';
import { CustomerDeliveryPlan } from './CustomerDeliveryPlan';

let mockPlan: { data?: OrderCustomerDelivery } = {};
jest.mock('../hooks/useOrderMaterials', () => ({ useCustomerDelivery: () => mockPlan }));
jest.mock('./CustomerDeliveryPlanModal', () => ({ CustomerDeliveryPlanModal: () => null }));

const plan = (patch: Partial<OrderCustomerDelivery>): OrderCustomerDelivery => ({
  orderId: 5,
  deliveryCount: null,
  deliveryDate: null,
  deliveryDateTo: null,
  receivedCount: 0,
  hasCustomerMaterials: true,
  ...patch,
});

describe('CustomerDeliveryPlan', () => {
  it('đơn không có NVL khách cấp và chưa khai lịch thì không hiện', () => {
    mockPlan = { data: plan({ hasCustomerMaterials: false }) };
    const { container } = render(<CustomerDeliveryPlan orderId={5} isClosed={false} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('khách không cấp NVL nào thì không hiện, kể cả khi đơn còn lịch giao cũ', () => {
    mockPlan = {
      data: plan({ hasCustomerMaterials: false, deliveryCount: 2, deliveryDate: '2026-12-01' }),
    };
    const { container } = render(<CustomerDeliveryPlan orderId={5} isClosed={false} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('có NVL khách cấp mà chưa khai lịch thì nhắc và cho khai', () => {
    mockPlan = { data: plan({}) };
    render(<CustomerDeliveryPlan orderId={5} isClosed={false} />);
    expect(screen.getByText(LABELS.customerDeliveryMissing)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: LABELS.editCustomerDelivery })).toBeInTheDocument();
  });

  it('đã khai thì hiện đã giao / số lần và khoảng ngày; đơn đóng thì không sửa', () => {
    mockPlan = {
      data: plan({
        deliveryCount: 2,
        deliveryDate: '2026-12-01',
        deliveryDateTo: '2026-12-10',
        receivedCount: 1,
      }),
    };
    render(<CustomerDeliveryPlan orderId={5} isClosed />);
    expect(screen.getByText('Đã giao 1 / 2 lần · 01/12/2026 – 10/12/2026')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
