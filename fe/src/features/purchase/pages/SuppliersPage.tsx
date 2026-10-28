import { useState } from 'react';
import { Button, Card, Popconfirm, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { SupplierFormModal } from '../components/SupplierFormModal';
import { PURCHASE_TEXT, SUPPLIER_STATUS_LABEL, SUPPLIER_STATUS_OPTIONS } from '../constants';
import { useSuppliers, useToggleSupplier } from '../hooks/usePurchase';
import type { Supplier, SupplierStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { keyword?: string; status?: SupplierStatus };

const text = PURCHASE_TEXT.supplierFields;

/** UC14 — Admin: danh sách nhà cung cấp, thêm / sửa, ngừng hợp tác hoặc hợp tác lại (không xóa). */
export function SuppliersPage() {
  const table = useTableQuery<Filters>({});
  const suppliers = useSuppliers(table.params);
  const toggle = useToggleSupplier();
  const [editing, setEditing] = useState<Supplier | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const openForm = (supplier: Supplier | null) => {
    setEditing(supplier);
    setFormOpen(true);
  };

  const columns: ColumnsType<Supplier> = [
    { title: text.name, dataIndex: 'supplierName' },
    { title: text.phone, dataIndex: 'phone', render: (v: string | null) => v ?? '—' },
    { title: text.email, dataIndex: 'email', render: (v: string | null) => v ?? '—' },
    { title: text.materialTypes, dataIndex: 'materialTypes', render: (v: string | null) => v ?? '—' },
    {
      title: text.status,
      dataIndex: 'status',
      render: (value: string) => <StatusTag status={value} labels={SUPPLIER_STATUS_LABEL} />,
    },
    { title: text.orderCount, dataIndex: 'purchaseOrderCount', align: 'right' },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space>
          <Button type="link" onClick={() => openForm(row)}>
            {MESSAGES.common.edit}
          </Button>
          {row.status === 'Active' ? (
            <Popconfirm
              title={PURCHASE_TEXT.discontinueConfirm}
              onConfirm={() => toggle.mutate({ id: row.supplierId, discontinue: true })}
            >
              <Button type="link" danger>
                {PURCHASE_TEXT.discontinue}
              </Button>
            </Popconfirm>
          ) : (
            <Button type="link" onClick={() => toggle.mutate({ id: row.supplierId, discontinue: false })}>
              {PURCHASE_TEXT.reactivate}
            </Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {PURCHASE_TEXT.suppliersTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{PURCHASE_TEXT.suppliersSubtitle}</Typography.Text>
      </div>

      <Card size="small">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <DebouncedSearch
              allowClear
              placeholder={PURCHASE_TEXT.searchSupplier}
              style={{ width: 300 }}
              onSearch={(value) => table.setFilters({ keyword: value.trim() || undefined })}
            />
            <FilterSelect value={table.filters.status}
              placeholder={PURCHASE_TEXT.allStatus}
              style={{ width: 180 }}
              options={SUPPLIER_STATUS_OPTIONS}
              onChange={(value: SupplierStatus | undefined) => table.setFilters({ status: value })}
            />
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
              {PURCHASE_TEXT.add}
            </Button>
          </Space>
          <QueryError error={suppliers.error} onRetry={() => void suppliers.refetch()} />
          <Table<Supplier>
            rowKey="supplierId"
            size="middle"
            columns={columns}
            dataSource={suppliers.data?.items ?? []}
            loading={suppliers.isFetching}
            pagination={table.pagination(suppliers.data?.totalItems)}
            locale={{ emptyText: MESSAGES.common.empty }}
            scroll={{ x: 'max-content' }}
          />
        </Space>
      </Card>

      <SupplierFormModal open={formOpen} supplier={editing} onClose={() => setFormOpen(false)} />
    </Space>
  );
}
