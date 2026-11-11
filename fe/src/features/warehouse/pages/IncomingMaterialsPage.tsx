import { useState } from 'react';
import { Badge, Space, Tabs, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { PageCard } from '@/components/common/PageCard';
import { IncomingToolbar } from '../components/IncomingToolbar';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import { ShortageTable } from '../components/ShortageTable';
import { SupplierIncomingTable } from '../components/SupplierIncomingTable';
import { CustomerIncomingTable } from '../components/CustomerIncomingTable';
import { closedShortColumns } from '../components/incomingMaterialColumns';
import {
  INCOMING_TAB,
  INCOMING_TEXT,
  LABELS,
  SHORTAGE_SCOPE_AWAITING,
  SHORTAGE_SCOPE_OPTIONS,
} from '../constants';
import { useIncomingFilters } from '../hooks/useIncomingFilters';
import { useIncomingMaterials } from '../hooks/useIncomingMaterials';
import { useIncomingOptions } from '../hooks/useIncomingOptions';
import { useShortageTab } from '../hooks/useShortageTab';
import { useIncomingRows } from '../hooks/useIncomingRows';
import type { ReceiptPreset } from '../types';

const text = INCOMING_TEXT;
const TAB = INCOMING_TAB;

const tabLabel = (label: string, count: number, color?: string) => (
  <Space size={6}>
    {label}
    <Badge count={count} showZero color={color} overflowCount={999} />
  </Space>
);

/**
 * Màn NVL chờ nhận của Thủ kho: NVL khách sẽ cấp (Quản đốc khai báo số lượng và số lần giao dự kiến ở "NVL của đơn"),
 * đơn mua đã đặt hàng mà kho chưa nhận đủ, đơn thiếu NVL chờ giao bù và đơn mua đã đóng thiếu. Mỗi nhóm một tab (có số
 * dòng) với bộ lọc riêng. Hàng về thì bấm Lập phiếu nhập: nguồn, đơn và số còn phải nhận được điền sẵn.
 */
export function IncomingMaterialsPage() {
  const incoming = useIncomingMaterials();
  const [preset, setPreset] = useState<ReceiptPreset | null>(null);
  const { filters, patch } = useIncomingFilters();
  const {
    list: replenish,
    table,
    search,
    awaitingOnly,
    setAwaitingOnly,
  } = useShortageTab(filters[TAB.REPLENISH].keyword);

  const raw = incoming.data;
  const { customer, supplier, closedShort } = useIncomingRows(raw, filters);
  const options = useIncomingOptions(raw);
  const loading = incoming.isFetching;
  const noResult = (tab: keyof typeof filters, empty: string) =>
    filters[tab].keyword.trim() || filters[tab].orderCode || filters[tab].supplierName
      ? text.noResult
      : empty;

  const items = [
    {
      key: TAB.CUSTOMER,
      label: tabLabel(text.tabCustomer, customer.length, 'blue'),
      children: (
        <>
          <IncomingToolbar
            placeholder={text.searchCustomer}
            onSearch={(keyword) => patch(TAB.CUSTOMER, { keyword })}
          >
            <FilterSelect
              value={filters[TAB.CUSTOMER].orderCode}
              placeholder={text.filterOrder}
              options={options.orders}
              onChange={(orderCode) => patch(TAB.CUSTOMER, { orderCode })}
              style={{ width: 220 }}
              showSearch
            />
          </IncomingToolbar>
          <CustomerIncomingTable
            rows={customer}
            loading={loading}
            emptyText={noResult(TAB.CUSTOMER, text.customerEmpty)}
            onReceive={setPreset}
          />
        </>
      ),
    },
    {
      key: TAB.SUPPLIER,
      label: tabLabel(text.tabSupplier, supplier.length, 'blue'),
      children: (
        <>
          <IncomingToolbar
            placeholder={text.searchPurchase}
            onSearch={(keyword) => patch(TAB.SUPPLIER, { keyword })}
          >
            <FilterSelect
              value={filters[TAB.SUPPLIER].supplierName}
              placeholder={text.filterSupplier}
              options={options.suppliers}
              onChange={(supplierName) => patch(TAB.SUPPLIER, { supplierName })}
              style={{ width: 240 }}
              showSearch
            />
            <FilterSelect
              value={filters[TAB.SUPPLIER].goodsStatus}
              placeholder={text.filterGoodsStatus}
              options={options.statuses}
              onChange={(goodsStatus) => patch(TAB.SUPPLIER, { goodsStatus })}
              style={{ width: 220 }}
            />
          </IncomingToolbar>
          <SupplierIncomingTable
            rows={supplier}
            loading={loading}
            emptyText={noResult(TAB.SUPPLIER, text.supplierEmpty)}
            onReceive={setPreset}
          />
        </>
      ),
    },
    {
      key: TAB.REPLENISH,
      label: tabLabel(text.tabReplenish, replenish.data?.totalItems ?? 0, 'orange'),
      children: (
        <>
          <IncomingToolbar
            placeholder={text.searchShortage}
            onSearch={(keyword) => patch(TAB.REPLENISH, { keyword })}
          >
            <FilterSelect
              value={awaitingOnly ? SHORTAGE_SCOPE_AWAITING : undefined}
              placeholder={LABELS.status}
              options={SHORTAGE_SCOPE_OPTIONS}
              onChange={(scope) => setAwaitingOnly(scope === SHORTAGE_SCOPE_AWAITING)}
              style={{ width: 240 }}
            />
          </IncomingToolbar>
          <ShortageTable
            rows={replenish.data?.items}
            loading={replenish.isFetching}
            pagination={table.pagination(replenish.data?.totalItems)}
            emptyText={search ? text.noResult : undefined}
          />
        </>
      ),
    },
    {
      key: TAB.CLOSED_SHORT,
      label: tabLabel(text.tabClosedShort, closedShort.length, 'gray'),
      children: (
        <>
          <IncomingToolbar
            placeholder={text.searchPurchase}
            onSearch={(keyword) => patch(TAB.CLOSED_SHORT, { keyword })}
          >
            <FilterSelect
              value={filters[TAB.CLOSED_SHORT].supplierName}
              placeholder={text.filterSupplier}
              options={options.closedSuppliers}
              onChange={(supplierName) => patch(TAB.CLOSED_SHORT, { supplierName })}
              style={{ width: 240 }}
              showSearch
            />
          </IncomingToolbar>
          <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>
            {text.closedShortHint}
          </Typography.Paragraph>
          <DataTable
            rowKey="purchaseOrderId"
            columns={closedShortColumns()}
            dataSource={closedShort}
            loading={loading}
            locale={{ emptyText: noResult(TAB.CLOSED_SHORT, text.closedShortEmpty) }}
          />
        </>
      ),
    },
  ];

  return (
    <PageCard title={text.title} description={text.subtitle}>
      <QueryError error={incoming.error} onRetry={() => void incoming.refetch()} />
      <Tabs items={items} />
      <ReceiptFormModal
        open={preset !== null}
        receipt={null}
        preset={preset}
        onClose={() => setPreset(null)}
      />
    </PageCard>
  );
}
