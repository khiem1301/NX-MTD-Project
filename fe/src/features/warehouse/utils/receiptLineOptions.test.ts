import type { OrderMaterial, PurchaseOrderLine } from '../types';
import {
  addableLineOptions,
  customerLineOptions,
  optionsForRow,
  purchaseLineOptions,
} from './receiptLineOptions';

const om = (id: number, patch: Partial<OrderMaterial>): OrderMaterial =>
  ({
    orderMaterialId: id,
    materialCode: `V${id}`,
    materialName: 'Vải',
    materialColor: '',
    unit: 'm',
    requiredQty: 10,
    customerQty: 10,
    receivedQty: 0,
    expectedDeliveryCount: 1,
    receivedCount: 0,
    ...patch,
  }) as OrderMaterial;

describe('customerLineOptions', () => {
  it('chỉ có NVL khách cấp, NVL phải mua không hiện', () => {
    const options = customerLineOptions([om(1, {}), om(2, { customerQty: 0 })]);
    expect(options.map((o) => o.value)).toEqual([1]);
  });

  it('đã nhận đủ số hoặc đủ số lần giao thì không chọn được', () => {
    const options = customerLineOptions([
      om(1, { receivedQty: 10, receivedCount: 1, expectedDeliveryCount: 2 }),
      om(2, { receivedQty: 4, receivedCount: 1 }),
      om(3, { receivedQty: 4, receivedCount: 1, expectedDeliveryCount: 2 }),
    ]);
    expect(options.map((o) => o.disabled)).toEqual([true, true, false]);
    expect(options[1]?.label).toContain('đã nhận đủ 1 lần giao');
  });
});

describe('purchaseLineOptions', () => {
  it('hết số còn phải nhận thì không chọn được', () => {
    const line = (id: number, receivedQty: number) =>
      ({
        purchaseOrderLineId: id,
        materialCode: 'C1',
        materialName: 'Chỉ',
        materialColor: '',
        unit: 'cuộn',
        orderedQty: 10,
        receivedQty,
        notReplenishedQty: 0,
      }) as PurchaseOrderLine;
    expect(purchaseLineOptions([line(1, 10), line(2, 3)]).map((o) => o.disabled)).toEqual([
      true,
      false,
    ]);
  });
});

describe('optionsForRow / addableLineOptions', () => {
  const options = customerLineOptions([om(1, {}), om(2, {}), om(3, {})]);

  it('NVL đã có ở dòng khác thì không chọn lại, dòng của chính nó vẫn giữ', () => {
    const row0 = optionsForRow(options, [1, 2], 0);
    expect(row0.find((o) => o.value === 1)?.disabled).toBe(false);
    expect(row0.find((o) => o.value === 2)?.disabled).toBe(true);
  });

  it('chỉ thêm được NVL chưa có trong phiếu', () => {
    expect(addableLineOptions(options, [1, undefined]).map((o) => o.value)).toEqual([2, 3]);
  });
});

describe('purchaseLineOptions theo số lần giao của cả đơn mua (DEV-ALL-95)', () => {
  const line = (id: number) =>
    ({
      purchaseOrderLineId: id,
      materialCode: `C${id}`,
      materialName: 'Chỉ',
      materialColor: '',
      unit: 'cuộn',
      orderedQty: 10,
      receivedQty: 0,
      notReplenishedQty: 0,
    }) as PurchaseOrderLine;

  it('đơn mua đã giao đủ số lần thì mọi dòng khóa, kể cả dòng chưa về lần nào', () => {
    const options = purchaseLineOptions([line(1), line(2)], 1, 1);
    expect(options.map((o) => o.disabled)).toEqual([true, true]);
    expect(options[1]?.label).toContain('đã nhận đủ 1 lần giao');
  });

  it('còn lượt giao thì chọn được', () => {
    expect(purchaseLineOptions([line(1)], 2, 1).map((o) => o.disabled)).toEqual([false]);
  });
});
