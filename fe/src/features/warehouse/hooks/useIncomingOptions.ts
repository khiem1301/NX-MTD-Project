import { useMemo } from 'react';
import { PO_GOODS_STATUS_LABEL } from '../constants';
import type { IncomingMaterials } from '../types';
import { toFilterOptions } from '../utils/filterIncoming';

/** Lựa chọn cho các ô lọc của màn NVL chờ nhận, lấy từ chính dữ liệu đang có (đơn hàng, nhà cung cấp, tình trạng). */
export const useIncomingOptions = (raw: IncomingMaterials | undefined) => {
  const customer = raw?.customer;
  const supplier = raw?.supplier;
  const closedShort = raw?.closedShort;
  return useMemo(
    () => ({
      orders: toFilterOptions((customer ?? []).map((r) => r.orderCode)),
      suppliers: toFilterOptions((supplier ?? []).map((r) => r.supplierName)),
      closedSuppliers: toFilterOptions((closedShort ?? []).map((r) => r.supplierName)),
      statuses: toFilterOptions((supplier ?? []).map((r) => r.goodsStatus)).map((o) => ({
        value: o.value,
        label: PO_GOODS_STATUS_LABEL[o.value]?.text ?? o.value,
      })),
    }),
    [customer, supplier, closedShort],
  );
};
