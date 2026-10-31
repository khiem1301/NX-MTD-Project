import { useState } from 'react';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { Button, Card, Popconfirm, Space, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
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

/** Danh mục khách hàng (UC09). Không xóa, chỉ ngừng hợp tác (không tạo được đơn mới). */
export function CustomersPage() {
  const [keyword, setKeyword] = useState<string>();
  const [status, setStatus] = useState<CustomerStatus>();
  const customers = useCustomers({ keyword, status });
  const toggle = useToggleCustomer();
  const [editing, setEditing] = useState<Customer | null>(null);
  const [open, setOpen] = useState(false);

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
      title: MESSAGES.common.actions,
      render: (_: unknown, c) => (
        <Space size={0} wrap>
          <Button
            type="link"
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(c);
              setOpen(true);
            }}
          >
            {MESSAGES.common.edit}
          </Button>
          {c.status === 'Active' ? (
            <Popconfirm
              title={MESSAGES.orders.customerDiscontinueConfirm}
              onConfirm={() => toggle.mutate({ id: c.customerId, discontinue: true })}
            >
              <Button type="link" danger>
                {MESSAGES.orders.customerDiscontinue}
              </Button>
            </Popconfirm>
          ) : (
            <Button
              type="link"
              onClick={() => toggle.mutate({ id: c.customerId, discontinue: false })}
            >
              {MESSAGES.orders.customerReactivate}
            </Button>
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
          onSearch={(k) => setKeyword(k.trim() || undefined)}
          style={{ width: 240 }}
        />
        <FilterSelect
          value={status}
          placeholder={MESSAGES.orders.allStatus}
          style={{ width: 200 }}
          options={STATUS_OPTIONS}
          onChange={setStatus}
        />
      </Space>
      <QueryError error={customers.error} onRetry={() => void customers.refetch()} />
      <DataTable<Customer>
        rowKey="customerId"
        loading={customers.isFetching}
        columns={columns}
        dataSource={customers.data ?? []}
        pagination={{ pageSize: 20 }}
      />
      <CustomerFormModal open={open} customer={editing} onClose={() => setOpen(false)} />
    </Card>
  );
}
