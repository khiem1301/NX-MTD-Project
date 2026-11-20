import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { OrderMaterial, PurchaseOrderLine } from '../types';

/** Một NVL chọn được trên dòng phiếu nhập; `outstanding` = số còn phải nhận để điền sẵn. */
export type ReceiptLineOption = {
  value: number;
  label: string;
  outstanding: number;
  disabled: boolean;
};

const materialText = (code: string, name: string, color: string | null, unit: string) =>
  `${code} ${name}${color ? ` - ${color}` : ''} (${unit})`;

/**
 * NVL khách cấp của đơn: chỉ NVL khách có cấp (NVL phải mua nhập theo đơn mua). NVL đã nhận đủ số khách cấp, hoặc đơn đã nhận đủ số
 * lần khách dự kiến giao (dùng chung cả đơn — DEV-ALL-95), thì hiện nhưng không chọn được (giao thêm phải qua phiếu bổ sung).
 */
export const customerLineOptions = (rows: OrderMaterial[]): ReceiptLineOption[] =>
  rows
    .filter((om) => om.customerQty > 0)
    .map((om) => {
      const outstanding = om.customerQty - om.receivedQty;
      const deliveries = om.expectedDeliveryCount ?? 1;
      const reached = om.receivedCount >= deliveries;
      const status = reached
        ? fill(LABELS.deliveriesReached, deliveries)
        : `${LABELS.outstandingQty} ${formatQty(Math.max(0, outstanding))}`;
      return {
        value: om.orderMaterialId,
        outstanding,
        disabled: outstanding <= 0 || reached,
        label: `${materialText(om.materialCode, om.materialName, om.materialColor, om.unit)} · ${status}`,
      };
    });

/**
 * Dòng đơn mua: hết số còn phải nhận (đặt − đã nhận − chốt không bù) thì không chọn được; đơn mua đã giao đủ số lần NCC dự kiến
 * (dùng chung cả đơn — DEV-ALL-95) thì mọi dòng đều khóa, hàng giao thêm nhận qua phiếu bổ sung.
 */
export const purchaseLineOptions = (
  lines: PurchaseOrderLine[],
  expectedDeliveryCount?: number | null,
  receivedCount = 0,
): ReceiptLineOption[] => {
  const deliveries = expectedDeliveryCount ?? 1;
  const reached = receivedCount >= deliveries;
  return lines.map((l) => {
    const outstanding = l.orderedQty - l.receivedQty - l.notReplenishedQty;
    const status = reached
      ? fill(LABELS.deliveriesReached, deliveries)
      : `${LABELS.outstandingQty} ${formatQty(Math.max(0, outstanding))}`;
    return {
      value: l.purchaseOrderLineId,
      outstanding,
      disabled: outstanding <= 0 || reached,
      label: `${materialText(l.materialCode, l.materialName, l.materialColor, l.unit)} · ${status}`,
    };
  });
};

/** Lựa chọn cho một dòng: NVL đã có ở dòng khác của phiếu thì không chọn lại được. */
export const optionsForRow = (
  options: ReceiptLineOption[],
  chosen: (number | undefined)[],
  rowIndex: number,
): ReceiptLineOption[] => {
  const taken = new Set(chosen.filter((v, i) => i !== rowIndex && v !== undefined));
  return options.map((o) => (taken.has(o.value) ? { ...o, disabled: true } : o));
};

/** NVL còn chọn thêm được vào phiếu: chưa có trong phiếu và còn nhận được. */
export const addableLineOptions = (
  options: ReceiptLineOption[],
  chosen: (number | undefined)[],
): ReceiptLineOption[] => options.filter((o) => !chosen.includes(o.value) && !o.disabled);
