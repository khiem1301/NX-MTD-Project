import { useState } from 'react';
import { Button, Checkbox, Popconfirm, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty } from '@/utils/format';
import { OperationFormModal } from '../components/OperationFormModal';
import { ProductSelect } from '../components/ProductSelect';
import { PRODUCTION_TEXT } from '../constants';
import { useOperations, useToggleOperation } from '../hooks/useProduction';
import type { Operation } from '../types';

const text = PRODUCTION_TEXT.operations;

/** UC25 (KI-01) — Kế toán lập công đoạn theo mã hàng; đơn giá của từng công đoạn lập ở màn "Đơn giá công đoạn". */
export function OperationsPage() {
  const [productId, setProductId] = useState<number | null>(null);
  const [showDiscontinued, setShowDiscontinued] = useState(false);
  const [editing, setEditing] = useState<Operation | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const operations = useOperations(productId, showDiscontinued);
  const toggle = useToggleOperation();

  const openForm = (operation: Operation | null) => {
    setEditing(operation);
    setFormOpen(true);
  };

  const columns: ColumnsType<Operation> = [
    { title: text.columns.sequence, dataIndex: 'stationSequence', width: 80 },
    { title: text.columns.code, dataIndex: 'operationCode' },
    { title: text.columns.name, dataIndex: 'operationName' },
    { title: text.columns.skill, dataIndex: 'skillLevel', render: (v: number | null) => v ?? '—' },
    {
      title: text.columns.standardTime,
      dataIndex: 'standardTimeSec',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatQty(v)),
    },
    { title: text.columns.teamType, dataIndex: 'teamType', render: (v: string) => text.teamType[v] ?? v },
    { title: text.columns.isFinal, dataIndex: 'isFinalStep', render: (v: boolean) => (v ? <Tag color="blue">Cuối</Tag> : '—') },
    {
      title: text.columns.rate,
      dataIndex: 'currentRate',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    { title: text.columns.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={text.status} /> },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0}>
          <Button type="link" onClick={() => openForm(row)}>
            {MESSAGES.common.edit}
          </Button>
          {row.status === 'Active' ? (
            <Popconfirm title={text.discontinueConfirm} onConfirm={() => toggle.mutate({ id: row.operationId, discontinue: true })}>
              <Button type="link" danger>
                {text.discontinue}
              </Button>
            </Popconfirm>
          ) : (
            <Button type="link" onClick={() => toggle.mutate({ id: row.operationId, discontinue: false })}>
              {text.reactivate}
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
          {text.title}
        </Typography.Title>
        <Typography.Text type="secondary">{text.subtitle}</Typography.Text>
      </div>

      <Space wrap>
        <ProductSelect value={productId ?? undefined} onChange={(v) => setProductId(v ?? null)} placeholder={PRODUCTION_TEXT.selectProduct} />
        <Checkbox checked={showDiscontinued} onChange={(e) => setShowDiscontinued(e.target.checked)}>
          {text.showDiscontinued}
        </Checkbox>
        <Button type="primary" icon={<PlusOutlined />} disabled={productId === null} onClick={() => openForm(null)}>
          {text.add}
        </Button>
      </Space>

      <QueryError error={operations.error} onRetry={() => void operations.refetch()} />

      <Table<Operation>
        rowKey="operationId"
        size="middle"
        columns={columns}
        dataSource={operations.data}
        loading={operations.isFetching}
        locale={{ emptyText: productId === null ? PRODUCTION_TEXT.selectProduct : MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        pagination={false}
      />

      {productId !== null && (
        <OperationFormModal open={formOpen} productId={productId} operation={editing} onClose={() => setFormOpen(false)} />
      )}
    </Space>
  );
}
