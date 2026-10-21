import { useState } from 'react';
import { Button, Card, Image, Input, Popconfirm, Select, Space, Table, Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag, type StatusLabel } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { OrderHistory } from '../components/OrderHistory';
import { ProductFormModal } from '../components/ProductFormModal';
import { useDiscontinueProduct, useProducts, useReactivateProduct } from '../hooks/useOrders';
import { useQuery } from '@tanstack/react-query';
import { productApi } from '../api/orderApi';
import type { ProductListItem, ProductStatus } from '../types';

type Filters = { keyword?: string; status?: ProductStatus };

const STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang dùng', color: 'green' },
  Discontinued: { text: 'Ngừng dùng', color: 'default' },
};

function ProductExpanded({ product }: { product: ProductListItem }) {
  const detail = useQuery({ queryKey: ['orders', 'products', 'detail', product.productId], queryFn: () => productApi.get(product.productId) });
  return (
    <>
      <p>{detail.data?.description ?? '—'}</p>
      <OrderHistory history={detail.data?.history ?? []} />
    </>
  );
}

/** UC10: mã hàng. Không xóa (GBR-14), chỉ ngừng dùng. */
export function ProductsPage() {
  const table = useTableQuery<Filters>({});
  const products = useProducts(table.params);
  const discontinue = useDiscontinueProduct();
  const reactivate = useReactivateProduct();
  const [editing, setEditing] = useState<ProductListItem | null>(null);
  const [open, setOpen] = useState(false);

  const columns: TableColumnsType<ProductListItem> = [
    { title: MESSAGES.orders.productCode, dataIndex: 'productCode' },
    { title: MESSAGES.orders.productName, dataIndex: 'productName' },
    {
      title: MESSAGES.orders.sampleImage,
      dataIndex: 'sampleImageUrl',
      render: (v: string | null) => (v ? <Image src={v} width={40} height={40} style={{ objectFit: 'cover' }} fallback="" /> : '—'),
    },
    { title: MESSAGES.orders.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={STATUS_TAG} /> },
    { title: MESSAGES.orders.used, dataIndex: 'isUsed', render: (v: boolean) => (v ? <Tag color="blue">{MESSAGES.orders.used}</Tag> : '—') },
    { title: MESSAGES.requests.createdAt, dataIndex: 'createdAt', render: formatDate },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, p) => (
        <Space>
          <Button size="small" onClick={() => { setEditing(p); setOpen(true); }}>{MESSAGES.common.edit}</Button>
          {p.status === 'Active' ? (
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
      extra={<Button type="primary" onClick={() => { setEditing(null); setOpen(true); }}>{MESSAGES.common.create}</Button>}
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search allowClear placeholder={MESSAGES.common.search} onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })} style={{ width: 240 }} />
        <Select<ProductStatus>
          allowClear
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
        expandable={{ expandedRowRender: (p) => <ProductExpanded product={p} /> }}
        locale={{ emptyText: MESSAGES.common.noData }}
      />
      <ProductFormModal open={open} product={editing} onClose={() => setOpen(false)} />
    </Card>
  );
}
