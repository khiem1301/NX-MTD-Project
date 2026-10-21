import { useState } from 'react';
import { Button, Card, Input, Space, Table, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { CustomerFormModal } from '../components/CustomerFormModal';
import { useCustomers } from '../hooks/useOrders';
import type { Customer } from '../types';

/** Danh mục khách hàng (UC09). Không xóa. */
export function CustomersPage() {
  const [keyword, setKeyword] = useState<string>();
  const customers = useCustomers(keyword);
  const [editing, setEditing] = useState<Customer | null>(null);
  const [open, setOpen] = useState(false);

  const columns: TableColumnsType<Customer> = [
    { title: MESSAGES.orders.customerName, dataIndex: 'customerName' },
    { title: MESSAGES.orders.phone, dataIndex: 'phone', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.orders.email, dataIndex: 'email', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.orders.address, dataIndex: 'defaultAddress', ellipsis: true, render: (v: string | null) => v ?? '—' },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, c) => (
        <Button size="small" onClick={() => { setEditing(c); setOpen(true); }}>{MESSAGES.common.edit}</Button>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.orders.customersTitle}
      extra={<Button type="primary" onClick={() => { setEditing(null); setOpen(true); }}>{MESSAGES.orders.addCustomer}</Button>}
    >
      <Space style={{ marginBottom: 16 }}>
        <Input.Search allowClear placeholder={MESSAGES.common.search} onSearch={(k) => setKeyword(k.trim() || undefined)} style={{ width: 240 }} />
      </Space>
      <QueryError error={customers.error} onRetry={() => void customers.refetch()} />
      <Table<Customer>
        rowKey="customerId"
        loading={customers.isFetching}
        columns={columns}
        dataSource={customers.data ?? []}
        pagination={{ pageSize: 20 }}
        locale={{ emptyText: MESSAGES.common.noData }}
      />
      <CustomerFormModal open={open} customer={editing} onClose={() => setOpen(false)} />
    </Card>
  );
}
