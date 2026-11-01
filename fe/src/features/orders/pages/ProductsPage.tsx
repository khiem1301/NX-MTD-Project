import { useState } from 'react';
import {
  PlusOutlined,
  EditOutlined,
  PlayCircleOutlined,
  StopOutlined,
  ProfileOutlined,
} from '@ant-design/icons';
import { Button, Card, Space, Tag, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { ProductImage } from '../components/ProductImage';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { ProductFormModal } from '../components/ProductFormModal';
import { ProductSpecModal } from '../components/ProductSpecModal';
import { useDiscontinueProduct, useProducts, useReactivateProduct } from '../hooks/useOrders';
import type { ProductListItem, ProductStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { PRODUCT_STATUS_TAG } from '../constants';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: ProductStatus };

/** UC10: mã hàng. Không xóa (GBR-14), chỉ ngừng sản xuất. */
export function ProductsPage() {
  const table = useTableQuery<Filters>({});
  const isAccountant = usePermission().hasRole(ROLES.Accountant); // Kế toán: chỉ xem danh sách và sửa định mức
  const products = useProducts(table.params);
  const discontinue = useDiscontinueProduct();
  const reactivate = useReactivateProduct();
  const [editing, setEditing] = useState<ProductListItem | null>(null);
  const [open, setOpen] = useState(false);
  const [specFor, setSpecFor] = useState<ProductListItem | null>(null);

  const columns: TableColumnsType<ProductListItem> = [
    {
      title: MESSAGES.orders.productCode,
      dataIndex: 'productCode',
    },
    { title: MESSAGES.orders.productName, dataIndex: 'productName' },
    {
      title: MESSAGES.orders.sampleImage,
      dataIndex: 'sampleImageUrl',
      render: (v: string | null) => (v ? <ProductImage value={v} /> : '—'),
    },
    {
      title: MESSAGES.orders.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={PRODUCT_STATUS_TAG} />,
    },
    {
      title: MESSAGES.orders.used,
      dataIndex: 'isUsed',
      render: (v: boolean) => (v ? <Tag color="blue">{MESSAGES.orders.used}</Tag> : '—'),
    },
    { title: MESSAGES.requests.createdAt, dataIndex: 'createdAt', render: formatDate },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, p) => (
        <Space size={0} wrap>
          <IconAction
            title={MESSAGES.orders.spec.button}
            icon={<ProfileOutlined />}
            onClick={() => setSpecFor(p)}
          />
          {!isAccountant && (
            <IconAction
              title={MESSAGES.common.edit}
              icon={<EditOutlined />}
              onClick={() => {
                setEditing(p);
                setOpen(true);
              }}
            />
          )}
          {isAccountant ? null : p.status === 'Active' ? (
            <ConfirmAction
              title={MESSAGES.orders.discontinueConfirm}
              onConfirm={() => discontinue.mutate(p.productId)}
            >
              <IconAction title={MESSAGES.orders.discontinue} icon={<StopOutlined />} danger />
            </ConfirmAction>
          ) : (
            <IconAction
              title={MESSAGES.orders.reactivate}
              icon={<PlayCircleOutlined />}
              onClick={() => reactivate.mutate(p.productId)}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.orders.productsTitle}
      extra={
        isAccountant ? undefined : (
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditing(null);
              setOpen(true);
            }}
          >
            {MESSAGES.common.create}
          </Button>
        )
      }
    >
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })}
              style={{ width: 240 }}
            />
            <FilterSelect<ProductStatus>
              value={table.filters.status}
              placeholder={MESSAGES.orders.status}
              style={{ width: 160 }}
              options={Object.entries(PRODUCT_STATUS_TAG).map(([value, l]) => ({
                value: value as ProductStatus,
                label: l.text,
              }))}
              onChange={(status) => table.setFilters({ status })}
            />
          </>
        }
      />
      <QueryError error={products.error} onRetry={() => void products.refetch()} />
      <DataTable<ProductListItem>
        rowKey="productId"
        loading={products.isFetching}
        columns={columns}
        dataSource={products.data?.items ?? []}
        pagination={table.pagination(products.data?.totalItems)}
      />
      <ProductFormModal open={open} product={editing} onClose={() => setOpen(false)} />
      <ProductSpecModal
        product={specFor}
        showHistory={!isAccountant}
        onClose={() => setSpecFor(null)}
      />
    </Card>
  );
}
