import { useState } from 'react';
import { Button, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, EditOutlined, PlayCircleOutlined, StopOutlined } from '@ant-design/icons';
import { IconAction } from '@/components/common/IconAction';
import { ReasonModal } from '@/components/common/ReasonModal';
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
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

type Filters = { keyword?: string; status?: SupplierStatus };

const text = PURCHASE_TEXT.supplierFields;

/** UC14 — Admin: danh sách nhà cung cấp, thêm / sửa, ngừng hợp tác hoặc hợp tác lại (không xóa). */
export function SuppliersPage() {
  const table = useTableQuery<Filters>({});
  const suppliers = useSuppliers(table.params);
  const toggle = useToggleSupplier();
  const [editing, setEditing] = useState<Supplier | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [stopping, setStopping] = useState<Supplier | null>(null);

  const openForm = (supplier: Supplier | null) => {
    setEditing(supplier);
    setFormOpen(true);
  };

  const columns: ColumnsType<Supplier> = [
    { title: text.name, dataIndex: 'supplierName' },
    { title: text.phone, dataIndex: 'phone', render: (v: string | null) => v ?? '—' },
    { title: text.email, dataIndex: 'email', render: (v: string | null) => v ?? '—' },
    {
      title: text.materialTypes,
      dataIndex: 'materialTypes',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: text.status,
      dataIndex: 'status',
      render: (value: string) => <StatusTag status={value} labels={SUPPLIER_STATUS_LABEL} />,
    },
    {
      title: text.discontinueReason,
      dataIndex: 'discontinueReason',
      ellipsis: true,
      render: (v: string | null) => v ?? '—',
    },
    { title: text.orderCount, dataIndex: 'purchaseOrderCount', align: 'right' },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0} wrap>
          <IconAction
            title={MESSAGES.common.edit}
            icon={<EditOutlined />}
            onClick={() => openForm(row)}
          />
          {row.status === 'Active' ? (
            <IconAction
              title={PURCHASE_TEXT.discontinue}
              icon={<StopOutlined />}
              danger
              onClick={() => setStopping(row)}
            />
          ) : (
            <IconAction
              title={PURCHASE_TEXT.reactivate}
              icon={<PlayCircleOutlined />}
              onClick={() => toggle.mutate({ id: row.supplierId })}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <PageCard
      title={PURCHASE_TEXT.suppliersTitle}
      description={PURCHASE_TEXT.suppliersSubtitle}
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {PURCHASE_TEXT.add}
        </Button>
      }
    >
      <Space wrap>
        <DebouncedSearch
          allowClear
          placeholder={PURCHASE_TEXT.searchSupplier}
          style={{ width: 300 }}
          onSearch={(value) => table.setFilters({ keyword: value.trim() || undefined })}
        />
        <FilterSelect
          value={table.filters.status}
          placeholder={PURCHASE_TEXT.allStatus}
          style={{ width: 180 }}
          options={SUPPLIER_STATUS_OPTIONS}
          onChange={(value: SupplierStatus | undefined) => table.setFilters({ status: value })}
        />
      </Space>
      <QueryError error={suppliers.error} onRetry={() => void suppliers.refetch()} />
      <DataTable<Supplier>
        rowKey="supplierId"
        columns={columns}
        dataSource={suppliers.data?.items ?? []}
        loading={suppliers.isFetching}
        pagination={table.pagination(suppliers.data?.totalItems)}
      />
      <ReasonModal
        open={stopping !== null}
        title={PURCHASE_TEXT.discontinueTitle}
        loading={toggle.isPending}
        onCancel={() => setStopping(null)}
        onSubmit={(reason) =>
          stopping &&
          toggle.mutate({ id: stopping.supplierId, reason }, { onSuccess: () => setStopping(null) })
        }
      />
      <SupplierFormModal open={formOpen} supplier={editing} onClose={() => setFormOpen(false)} />
    </PageCard>
  );
}
