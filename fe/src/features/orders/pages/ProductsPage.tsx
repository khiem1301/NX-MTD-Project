import { useState } from 'react';
import { Button, Card, Popconfirm, Space, Table, Tag, type TableColumnsType } from 'antd';
import { ProductImage } from '../components/ProductImage';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag, type StatusLabel } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { AuditHistory } from '@/components/common/AuditHistory';
import { ProductFormModal } from '../components/ProductFormModal';
import { ProductSpecDrawer } from '../components/ProductSpecDrawer';
import { useDiscontinueProduct, useProducts, useReactivateProduct } from '../hooks/useOrders';
import { useQuery } from '@tanstack/react-query';
import { productApi } from '../api/orderApi';
import type { ProductListItem, ProductStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { keyword?: string; status?: ProductStatus };

const STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang sản xuất', color: 'green' },
  Discontinued: { text: 'Ngừng sản xuất', color: 'default' },
};

function ProductExpanded({ product }: { product: ProductListItem }) {
  const detail = useQuery({ queryKey: ['orders', 'products', 'detail', product.productId], queryFn: () => productApi.get(product.productId) });
  return (
    <>
      <p>{detail.data?.description ?? '—'}</p>
      <AuditHistory history={detail.data?.history ?? []} />
    </>
  );
}

/** UC10: mã hàng. Không xóa (GBR-14), chỉ ngừng sản xuất. */
export function ProductsPage() {
  const table = useTableQuery<Filters>({});
  const isAccountant = usePermission().hasRole(ROLES.Accountant);   // Kế toán: chỉ xem danh sách và sửa định mức
  const products = useProducts(table.params);
  const discontinue = useDiscontinueProduct();
  const reactivate = useReactivateProduct();
  const [editing, setEditing] = useState<ProductListItem | null>(null);
  const [open, setOpen] = useState(false);
  const [specFor, setSpecFor] = useState<ProductListItem | null>(null);

  const columns: TableColumnsType<ProductListItem> = [
    { title: MESSAGES.orders.productCode, dataIndex: 'productCode' },
    { title: MESSAGES.orders.productName, dataIndex: 'productName' },
    {
      title: MESSAGES.orders.sampleImage,
      dataIndex: 'sampleImageUrl',
      render: (v: string | null) => (v ? <ProductImage value={v} /> : '—'),
    },
    { title: MESSAGES.orders.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={STATUS_TAG} /> },
    { title: MESSAGES.orders.used, dataIndex: 'isUsed', render: (v: boolean) => (v ? <Tag color="blue">{MESSAGES.orders.used}</Tag> : '—') },
    { title: MESSAGES.requests.createdAt, dataIndex: 'createdAt', render: formatDate },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, p) => (
        <Space>
          {!isAccountant && <Button size="small" onClick={() => { setEditing(p); setOpen(true); }}>{MESSAGES.common.edit}</Button>}
          <Button size="small" onClick={() => setSpecFor(p)}>{MESSAGES.orders.spec.button}</Button>
          {isAccountant ? null : p.status === 'Active' ? (
            <Popconfirm title={MESSAGES.orders.discontinueConfirm} onConfirm={() => discontinue.mutate(p.productId)}>
              <Button size="small" danger>{MESSAGES.orders.discontinue}</Button>
            </Popconfirm>
          ) : (
            <Button size="small" onClick={() => reactivate.mutate(p.productId)}>{MESSAGES.orders.reactivate}</Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.orders.productsTitle}
      extra={isAccountant ? undefined : <Button type="primary" onClick={() => { setEditing(null); setOpen(true); }}>{MESSAGES.common.create}</Button>}
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch allowClear placeholder={MESSAGES.common.search} onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })} style={{ width: 240 }} />
        <FilterSelect<ProductStatus> value={table.filters.status}
          placeholder={MESSAGES.orders.status}
          style={{ width: 160 }}
          options={Object.entries(STATUS_TAG).map(([value, l]) => ({ value: value as ProductStatus, label: l.text }))}
          onChange={(status) => table.setFilters({ status })}
        />
      </Space>
      <QueryError error={products.error} onRetry={() => void products.refetch()} />
      <Table<ProductListItem>
        rowKey="productId"
        loading={products.isFetching}
        columns={columns}
        dataSource={products.data?.items ?? []}
        pagination={table.pagination(products.data?.totalItems)}
        expandable={isAccountant ? undefined : { expandedRowRender: (p) => <ProductExpanded product={p} /> }}
        locale={{ emptyText: MESSAGES.common.noData }}
      />
      <ProductFormModal open={open} product={editing} onClose={() => setOpen(false)} />
      <ProductSpecDrawer product={specFor} onClose={() => setSpecFor(null)} />
    </Card>
  );
}
