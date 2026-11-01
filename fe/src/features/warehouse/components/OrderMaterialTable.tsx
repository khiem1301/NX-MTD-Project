import type { ReactNode } from 'react';
import type { TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ORDER_MATERIAL_FILTER_TEXT as text } from '../constants';
import { useOrderMaterialPage } from '../hooks/useOrderMaterials';
import {
  ORDER_MATERIAL_RECEIPT_STATE,
  ORDER_MATERIAL_SUPPLY,
  type OrderMaterial,
  type OrderMaterialReceiptState,
  type OrderMaterialSupply,
} from '../types';

type OrderMaterialTableProps = {
  orderId: number;
  columns: TableColumnsType<OrderMaterial>;
  /** Nút bên phải thanh công cụ. */
  actions?: ReactNode;
};

type Filters = {
  keyword?: string;
  supply?: OrderMaterialSupply;
  receiptState?: OrderMaterialReceiptState;
};

const SUPPLY_OPTIONS = [
  { value: ORDER_MATERIAL_SUPPLY.Customer, label: text.supplyCustomer },
  { value: ORDER_MATERIAL_SUPPLY.Purchase, label: text.supplyPurchase },
];

const RECEIPT_OPTIONS = [
  { value: ORDER_MATERIAL_RECEIPT_STATE.Pending, label: text.receiptPending },
  { value: ORDER_MATERIAL_RECEIPT_STATE.Done, label: text.receiptDone },
];

/** Bảng NVL của một đơn: tìm kiếm, lọc nguồn cấp và tình trạng nhập kho, phân trang ở server. */
export function OrderMaterialTable({ orderId, columns, actions }: OrderMaterialTableProps) {
  const table = useTableQuery<Filters>({});
  const page = useOrderMaterialPage({ ...table.params, orderId });

  return (
    <>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={text.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })}
              style={{ width: 260 }}
            />
            <FilterSelect<OrderMaterialSupply>
              value={table.filters.supply}
              placeholder={text.supply}
              style={{ width: 170 }}
              options={SUPPLY_OPTIONS}
              onChange={(supply) => table.setFilters({ supply })}
            />
            <FilterSelect<OrderMaterialReceiptState>
              value={table.filters.receiptState}
              placeholder={text.receipt}
              style={{ width: 170 }}
              options={RECEIPT_OPTIONS}
              onChange={(receiptState) => table.setFilters({ receiptState })}
            />
          </>
        }
        actions={actions}
      />
      <QueryError error={page.error} onRetry={() => void page.refetch()} />
      <DataTable
        rowKey="orderMaterialId"
        columns={columns}
        dataSource={page.data?.items}
        loading={page.isFetching}
        pagination={table.pagination(page.data?.totalItems)}
      />
    </>
  );
}
