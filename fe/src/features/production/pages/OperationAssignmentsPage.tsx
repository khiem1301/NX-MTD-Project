import { useState } from 'react';
import { Button, Popconfirm, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CopyOutlined, PlusOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useAssignments, useCloseAssignment } from '../hooks/useProduction';
import type { OperationAssignment } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AssignModal } from '../components/AssignModal';
import { ReplaceModal } from '../components/ReplaceModal';
import { CopyModal } from '../components/CopyModal';

const text = PRODUCTION_TEXT.assignments;

/** UC52 (KI-03) — tổ trưởng / tổ phó may phân công công nhân × công đoạn theo đơn hàng; đổi phân công giữ lịch sử. */
export function OperationAssignmentsPage() {
  const [orderId, setOrderId] = useState<number | null>(null);
  const [assignOpen, setAssignOpen] = useState(false);
  const [copyOpen, setCopyOpen] = useState(false);
  const [replacing, setReplacing] = useState<OperationAssignment | null>(null);
  const assignments = useAssignments(orderId);
  const close = useCloseAssignment();

  const columns: ColumnsType<OperationAssignment> = [
    { responsive: ['md'], title: text.columns.product, dataIndex: 'productCode' },
    {
      title: text.columns.operation,
      key: 'op',
      render: (_, r) => `${r.operationCode} - ${r.operationName}`,
    },
    {
      title: text.columns.worker,
      key: 'worker',
      render: (_, r) => `${r.workerCode} - ${r.workerName}`,
    },
    {
      responsive: ['md'],
      title: text.columns.validFrom,
      dataIndex: 'validFrom',
      render: (v: string) => formatDate(v),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0} wrap>
          <Button type="link" onClick={() => setReplacing(row)}>
            {text.replace}
          </Button>
          <Popconfirm
            title={text.closeConfirm}
            onConfirm={() => close.mutate(row.operationAssignmentId)}
          >
            <Button type="link" danger>
              {text.close}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <PageCard
      title={text.title}
      description={text.subtitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={orderId === null}
          onClick={() => setAssignOpen(true)}
        >
          {text.assign}
        </Button>
      }
    >
      <Space wrap>
        <OrderSelect
          value={orderId ?? undefined}
          onChange={(v) => setOrderId(v ?? null)}
          placeholder={PRODUCTION_TEXT.selectOrder}
        />
        <Button
          icon={<CopyOutlined />}
          disabled={orderId === null}
          onClick={() => setCopyOpen(true)}
        >
          {text.copy}
        </Button>
      </Space>

      <QueryError error={assignments.error} onRetry={() => void assignments.refetch()} />

      <DataTable<OperationAssignment>
        rowKey="operationAssignmentId"
        size="small"
        columns={columns}
        dataSource={assignments.data}
        loading={assignments.isFetching}
        locale={{ emptyText: orderId === null ? PRODUCTION_TEXT.selectFirst : text.empty }}
        scroll={{ x: 'max-content' }}
        pagination={false}
      />

      {orderId !== null && (
        <>
          <AssignModal open={assignOpen} orderId={orderId} onClose={() => setAssignOpen(false)} />
          <CopyModal open={copyOpen} toOrderId={orderId} onClose={() => setCopyOpen(false)} />
        </>
      )}
      <ReplaceModal target={replacing} onClose={() => setReplacing(null)} />
    </PageCard>
  );
}
