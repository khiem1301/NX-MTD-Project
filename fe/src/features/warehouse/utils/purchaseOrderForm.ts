import type { Dayjs } from 'dayjs';
import { toApiDate } from '@/utils/format';
import type { CreatePurchaseOrderPayload } from '../types';
import { isNewMaterial, newMaterialName } from './newMaterial';

export type PurchaseOrderLineValues = {
  material?: number | string;
  unit?: string;
  newMaterialColor?: string;
  orderedQty: number;
  unitPrice: number;
};

export type PurchaseOrderFormValues = {
  orderId?: number;
  supplierId?: number;
  orderDate?: Dayjs;
  expectedDeliveryCount?: number;
  note?: string;
  lines: PurchaseOrderLineValues[];
};

export const EMPTY_PURCHASE_LINE: PurchaseOrderLineValues = { orderedQty: 1, unitPrice: 0 };

/** Dòng NVL chưa có trong danh mục gửi tên + đơn vị + màu để BE thêm mới; NVL có sẵn gửi materialId. */
export const toPurchaseOrderPayload = (
  values: PurchaseOrderFormValues,
): CreatePurchaseOrderPayload => ({
  orderId: values.orderId,
  supplierId: values.supplierId as number,
  orderDate: toApiDate(values.orderDate as Dayjs),
  expectedDeliveryCount: values.expectedDeliveryCount as number,
  note: values.note,
  lines: values.lines.map(({ material, unit, newMaterialColor, ...rest }) =>
    isNewMaterial(material)
      ? {
          ...rest,
          newMaterialName: newMaterialName(material),
          newMaterialUnit: unit,
          newMaterialColor,
        }
      : { ...rest, materialId: material },
  ),
});
