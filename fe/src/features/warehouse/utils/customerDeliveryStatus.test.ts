import dayjs from 'dayjs';
import type { OrderCustomerDelivery } from '../types';
import { CUSTOMER_DELIVERY_STATUS, customerDeliveryStatus } from './customerDeliveryStatus';

const today = dayjs('2026-11-11');
const plan = (patch: Partial<OrderCustomerDelivery>): OrderCustomerDelivery => ({
  orderId: 1,
  deliveryCount: 2,
  deliveryDate: '2026-11-20',
  deliveryDateTo: '2026-11-30',
  receivedCount: 0,
  hasCustomerMaterials: true,
  ...patch,
});

describe('customerDeliveryStatus', () => {
  it('đã nhận đủ số lần (chưa khai coi như 1 lần) thì là FULL', () => {
    expect(customerDeliveryStatus(plan({ receivedCount: 2 }), today)).toBe(
      CUSTOMER_DELIVERY_STATUS.FULL,
    );
    expect(customerDeliveryStatus(plan({ deliveryCount: null, receivedCount: 1 }), today)).toBe(
      CUSTOMER_DELIVERY_STATUS.FULL,
    );
  });

  it('còn lượt mà ngày giao cuối đã qua thì là LATE', () => {
    expect(customerDeliveryStatus(plan({ deliveryDateTo: '2026-11-10' }), today)).toBe(
      CUSTOMER_DELIVERY_STATUS.LATE,
    );
    expect(
      customerDeliveryStatus(
        plan({ deliveryCount: 1, deliveryDate: '2026-11-01', deliveryDateTo: null }),
        today,
      ),
    ).toBe(CUSTOMER_DELIVERY_STATUS.LATE);
  });

  it('khách không cấp NVL thì không nhắc gì', () => {
    expect(
      customerDeliveryStatus(
        plan({ hasCustomerMaterials: false, deliveryDateTo: '2026-11-10' }),
        today,
      ),
    ).toBeNull();
  });

  it('còn lượt và chưa tới ngày thì bình thường', () => {
    expect(customerDeliveryStatus(plan({}), today)).toBeNull();
    expect(customerDeliveryStatus(plan({ deliveryDateTo: '2026-11-11' }), today)).toBeNull();
    expect(customerDeliveryStatus(undefined, today)).toBeNull();
  });
});
