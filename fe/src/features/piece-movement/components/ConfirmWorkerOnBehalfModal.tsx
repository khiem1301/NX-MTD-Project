import { Descriptions, Form, Input, Modal, Table } from 'antd';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { HintTip } from '@/components/common/HintTip';
import { MOVEMENT_STATUS, MOVEMENT_TYPE_LABEL, PM_LABELS } from '../constants';
import { useAllocations, useConfirmWorkerOnBehalf } from '../hooks/usePieceMovement';
import type { WorkerAllocationSummary } from '../types';
import { negated, signedQty } from '../utils/signedQty';

type Props = { group: WorkerAllocationSummary | null; onClose: () => void };

/** Số phiếu chờ hiện trong modal tối đa bằng số phiếu BE trả một trang; xác nhận hộ vẫn áp cho mọi phiếu chờ của công nhân. */
const PENDING_LIST_SIZE = 100;

/** DEV-ALL-182: tổ trưởng xác nhận hộ một lần mọi phiếu đang chờ của công nhân; lý do và cảnh báo nằm trong modal. */
export function ConfirmWorkerOnBehalfModal({ group, onClose }: Props) {
  const [form] = Form.useForm<{ onBehalfReason: string }>();
  const confirm = useConfirmWorkerOnBehalf();
  const workerId = group?.workerId;
  const pending = useAllocations(
    {
      workerId,
      status: MOVEMENT_STATUS.PENDING,
      page: 1,
      pageSize: PENDING_LIST_SIZE,
    },
    workerId !== undefined,
  );

  const handleFinish = (values: { onBehalfReason: string }) => {
    if (!group) return;
    // Xác nhận hộ là ngoại lệ (công nhân lẽ ra tự xác nhận): cảnh báo trước khi gửi.
    Modal.confirm({
      title: PM_LABELS.onBehalfWarnTitle,
      content: PM_LABELS.onBehalfWarnContent,
      okText: PM_LABELS.onBehalfWarnOk,
      cancelText: MESSAGES.common.cancel,
      okButtonProps: { danger: true },
      onOk: () =>
        confirm.mutate(
          { workerId: group.workerId, body: { onBehalfReason: values.onBehalfReason.trim() } },
          { onSuccess: onClose },
        ),
    });
  };

  return (
    <Modal
      open={group !== null}
      title={PM_LABELS.confirmWorkerOnBehalf}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={confirm.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      width={720}
      destroyOnHidden
    >
      {group && (
        <Form
          form={form}
          layout="vertical"
          preserve={false}
          disabled={confirm.isPending}
          onFinish={handleFinish}
        >
          <HintTip text={PM_LABELS.workerOnBehalfHint} />
          <Descriptions column={1} size="small" bordered style={{ marginBottom: 16 }}>
            <Descriptions.Item label={PM_LABELS.worker}>
              {`${group.workerCode} - ${group.workerName}`}
            </Descriptions.Item>
            <Descriptions.Item label={PM_LABELS.pendingConfirmTitle}>
              {fill(
                PM_LABELS.workerOnBehalfSummary,
                group.pendingCount,
                group.pendingHandoverQty,
                negated(group.pendingRetrievalQty),
              )}
            </Descriptions.Item>
          </Descriptions>
          <Table
            size="small"
            pagination={false}
            rowKey="workAllocationId"
            dataSource={pending.data?.items}
            loading={pending.isFetching}
            scroll={{ y: 240 }}
            style={{ marginBottom: 16 }}
            columns={[
              {
                title: PM_LABELS.type,
                dataIndex: 'movementType',
                render: (v: string) => MOVEMENT_TYPE_LABEL[v],
              },
              {
                title: PM_LABELS.orderLine,
                key: 'line',
                render: (_, m) => `${m.orderCode} · ${m.productCode} ${m.color}-${m.sizeCode}`,
              },
              {
                title: PM_LABELS.operation,
                key: 'operation',
                render: (_, m) => `${m.operationCode} - ${m.operationName}`,
              },
              {
                title: PM_LABELS.quantity,
                dataIndex: 'quantity',
                align: 'right',
                render: (v: number, m) => signedQty(m.movementType, v),
              },
            ]}
          />
          <Form.Item
            name="onBehalfReason"
            label={PM_LABELS.onBehalfReason}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}
          >
            <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
