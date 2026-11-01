import { useState } from 'react';
import { PlusOutlined, EditOutlined, PlayCircleOutlined, StopOutlined } from '@ant-design/icons';
import { Button, Card, Space, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ReasonModal } from '@/components/common/ReasonModal';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { CustomerFormModal } from '../components/CustomerFormModal';
import { useCustomers, useToggleCustomer } from '../hooks/useOrders';
import type { Customer, CustomerStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { StatusTag, type StatusLabel } from '@/components/common/StatusTag';
import { DataTable } from '@/components/common/DataTable';

const STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: MESSAGES.orders.customerActive, color: 'green' },
  Discontinued: { text: MESSAGES.orders.customerInactive, color: 'default' },
};
const STATUS_OPTIONS = (Object.keys(STATUS_TAG) as CustomerStatus[]).map((v) => ({
  value: v,
  label: (STATUS_TAG[v] as StatusLabel).text,
}));

type Filters = { keyword?: string; status?: CustomerStatus };

/** Danh mục khách hàng (UC09). Không xóa, chỉ ngừng hợp tác (không tạo được đơn mới). */
export function CustomersPage() {
  const table = useTableQuery<Filters>({});
  const customers = useCustomers(table.params);
  const toggle = useToggleCustomer();
  const [editing, setEditing] = useState<Customer | null>(null);
  const [open, setOpen] = useState(false);
  const [stopping, setStopping] = useState<Customer | null>(null);

  const columns: TableColumnsType<Customer> = [
    { title: MESSAGES.orders.customerName, dataIndex: 'customerName' },
    { title: MESSAGES.orders.phone, dataIndex: 'phone', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.orders.email, dataIndex: 'email', render: (v: string | null) => v ?? '—' },
    {
      title: MESSAGES.orders.address,
      dataIndex: 'defaultAddress',
      ellipsis: true,
      render: (v: string | null) => v ?? '—',
    },
    {
      title: MESSAGES.orders.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={STATUS_TAG} />,
    },
    {
      title: MESSAGES.orders.customerDiscontinueReason,
      dataIndex: 'discontinueReason',
      ellipsis: true,
      render: (v: string | null) => v ?? '—',
    },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, c) => (
        <Space size={0} wrap>
          <IconAction
            title={MESSAGES.common.edit}
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(c);
              setOpen(true);
            }}
          />
          {c.status === 'Active' ? (
            <IconAction
              title={MESSAGES.orders.customerDiscontinue}
              icon={<StopOutlined />}
              danger
              onClick={() => setStopping(c)}
            />
          ) : (
            <IconAction
              title={MESSAGES.orders.customerReactivate}
              icon={<PlayCircleOutlined />}
              onClick={() => toggle.mutate({ id: c.customerId })}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.orders.customersTitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          {MESSAGES.orders.addCustomer}
        </Button>
      }
    >
      <Space style={{ marginBottom: 16 }}>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(k) => table.setFilters({ keyword: k.trim() || undefined })}
          style={{ width: 240 }}
        />
        <FilterSelect
          value={table.filters.status}
          placeholder={MESSAGES.orders.allStatus}
          style={{ width: 200 }}
          options={STATUS_OPTIONS}
          onChange={(status) => table.setFilters({ status })}
        />
      </Space>
      <QueryError error={customers.error} onRetry={() => void customers.refetch()} />
      <DataTable<Customer>
        rowKey="customerId"
        loading={customers.isFetching}
        columns={columns}
        dataSource={customers.data?.items ?? []}
        pagination={table.pagination(customers.data?.totalItems)}
      />
      <ReasonModal
        open={stopping !== null}
        title={MESSAGES.orders.customerDiscontinueConfirm}
        loading={toggle.isPending}
        onCancel={() => setStopping(null)}
        onSubmit={(reason) =>
          stopping &&
          toggle.mutate({ id: stopping.customerId, reason }, { onSuccess: () => setStopping(null) })
        }
      />
      <CustomerFormModal open={open} customer={editing} onClose={() => setOpen(false)} />
    </Card>
  );
}
