import { useMemo } from 'react';
import { INCOMING_TAB } from '../constants';
import type { IncomingMaterials } from '../types';
import { filterCustomer, filterPurchase, type IncomingFilter } from '../utils/filterIncoming';
import { groupByOrder } from '../utils/groupByOrder';
import { groupByPurchaseOrder } from '../utils/groupByPurchaseOrder';

type TabFilters = Record<(typeof INCOMING_TAB)[keyof typeof INCOMING_TAB], IncomingFilter>;

/** Dòng hiển thị của ba tab lấy từ danh sách chờ nhận: đã lọc theo bộ lọc của tab, đơn mua gộp theo đơn. */
export const useIncomingRows = (raw: IncomingMaterials | undefined, filters: TabFilters) => {
  const customerRows = raw?.customer;
  const supplierRows = raw?.supplier;
  const closedRows = raw?.closedShort;
  const customer = useMemo(
    () => groupByOrder(filterCustomer(customerRows ?? [], filters[INCOMING_TAB.CUSTOMER])),
    [customerRows, filters],
  );
  const supplier = useMemo(
    () => groupByPurchaseOrder(filterPurchase(supplierRows ?? [], filters[INCOMING_TAB.SUPPLIER])),
    [supplierRows, filters],
  );
  const closedShort = useMemo(
    () =>
      groupByPurchaseOrder(filterPurchase(closedRows ?? [], filters[INCOMING_TAB.CLOSED_SHORT])),
    [closedRows, filters],
  );
  return { customer, supplier, closedShort };
};
