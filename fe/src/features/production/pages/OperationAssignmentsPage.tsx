import { useState } from 'react';
import { Button, Form, Modal, Popconfirm, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CopyOutlined, PlusOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { formatDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import {
  useAssignableOperations,
  useAssignOperations,
  useAssignments,
  useCloseAssignment,
  useCopyAssignments,
  useReplaceAssignment,
} from '../hooks/useProduction';
import type { OperationAssignment } from '../types';

const text = PRODUCTION_TEXT.assignments;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

/** Danh sách công nhân đang làm của tổ mình, dùng cho các ô chọn công nhân. */
function useWorkerOptions() {
  const { user } = useAuth();
  const members = useTeamMemberLookup(user?.teamId ?? null);
  const options = (members.data ?? [])
    .filter((m) => m.role === 'Worker')
    .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }));
  return { options, loading: members.isLoading };
}

function AssignModal({ open, orderId, onClose }: { open: boolean; orderId: number; onClose: () => void }) {
  const [form] = Form.useForm<{ operationIds: number[]; workerId: number }>();
  const operations = useAssignableOperations(orderId);
  const workers = useWorkerOptions();
  const assign = useAssignOperations();

  return (
    <Modal
      open={open}
      title={text.assignTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={assign.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) => assign.mutate({ orderId, ...values }, { onSuccess: onClose })}
      >
        <Form.Item name="operationIds" label={text.form.operations} rules={required}>
          <Select
            mode="multiple"
            showSearch
            optionFilterProp="label"
            loading={operations.isLoading}
            options={(operations.data ?? []).map((o) => ({
              value: o.operationId,
              label: `${o.productCode} · ${o.stationSequence}. ${o.operationCode} - ${o.operationName}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="workerId" label={text.form.worker} rules={required}>
          <Select showSearch optionFilterProp="label" loading={workers.loading} options={workers.options} />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function ReplaceModal({ target, onClose }: { target: OperationAssignment | null; onClose: () => void }) {
  const [form] = Form.useForm<{ newWorkerId: number }>();
  const workers = useWorkerOptions();
  const replace = useReplaceAssignment();

  return (
    <Modal
      open={target !== null}
      title={`${text.replaceTitle}: ${target?.operationCode ?? ''}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={replace.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) =>
          target && replace.mutate({ id: target.operationAssignmentId, newWorkerId: values.newWorkerId }, { onSuccess: onClose })
        }
      >
        <Form.Item name="newWorkerId" label={text.form.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={workers.loading}
            options={workers.options.filter((o) => o.value !== target?.workerId)}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}

function CopyModal({ open, toOrderId, onClose }: { open: boolean; toOrderId: number; onClose: () => void }) {
  const [form] = Form.useForm<{ fromOrderId: number }>();
  const copy = useCopyAssignments();

  return (
    <Modal
      open={open}
      title={text.copyTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={copy.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary">{text.copyHint}</Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) => copy.mutate({ fromOrderId: values.fromOrderId, toOrderId }, { onSuccess: onClose })}
      >
        <Form.Item name="fromOrderId" label={text.copyFrom} rules={required}>
          <OrderSelect width="100%" />
        </Form.Item>
      </Form>
    </Modal>
  );
}

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
    { title: text.columns.operation, key: 'op', render: (_, r) => `${r.operationCode} - ${r.operationName}` },
    { title: text.columns.worker, key: 'worker', render: (_, r) => `${r.workerCode} - ${r.workerName}` },
    { responsive: ['md'], title: text.columns.validFrom, dataIndex: 'validFrom', render: (v: string) => formatDate(v) },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0}>
          <Button type="link" onClick={() => setReplacing(row)}>
            {text.replace}
          </Button>
          <Popconfirm title={text.closeConfirm} onConfirm={() => close.mutate(row.operationAssignmentId)}>
            <Button type="link" danger>
              {text.close}
            </Button>
          </Popconfirm>
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
        <OrderSelect value={orderId ?? undefined} onChange={(v) => setOrderId(v ?? null)} placeholder={PRODUCTION_TEXT.selectOrder} />
        <Button type="primary" icon={<PlusOutlined />} disabled={orderId === null} onClick={() => setAssignOpen(true)}>
          {text.assign}
        </Button>
        <Button icon={<CopyOutlined />} disabled={orderId === null} onClick={() => setCopyOpen(true)}>
          {text.copy}
        </Button>
      </Space>

      <QueryError error={assignments.error} onRetry={() => void assignments.refetch()} />

      <Table<OperationAssignment>
        rowKey="operationAssignmentId"
        size="middle"
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
    </Space>
  );
}
