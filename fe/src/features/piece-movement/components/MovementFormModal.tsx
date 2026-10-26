import { useState } from 'react';
import { Descriptions, Form, InputNumber, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { MOVEMENT_TYPE, PM_LABELS, RETRIEVAL_REASON_LABEL } from '../constants';
import { useCreateAllocation } from '../hooks/usePieceMovement';
import type { OperationProgress, WorkerAllocationLine } from '../types';

export type AllocationTarget =
  | { kind: 'HANDOVER'; progress: OperationProgress; pieceLimit: number }
  | { kind: 'RETRIEVAL'; progress: OperationProgress; worker: WorkerAllocationLine };

type Props = { target: AllocationTarget | null; onClose: () => void };
type FormValues = {
  workerId?: number;
  quantity?: number;
  pieceQty?: number;
  retrievalReason?: string;
};

const REASON_OPTIONS = Object.entries(RETRIEVAL_REASON_LABEL).map(([value, label]) => ({
  value,
  label,
}));

/**
 * UC53: tổ trưởng giao việc (số lần làm một công đoạn, kèm BTP nếu công đoạn dùng BTP) hoặc thu hồi việc từ một công nhân.
 * Số lần bị chặn theo số còn cần giao / còn thu hồi được; công nhân xác nhận sau đó (UC60).
 */
export function MovementFormModal({ target, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const { user } = useAuth();
  const handover = target?.kind === 'HANDOVER';
  const members = useTeamMemberLookup(handover ? (user?.teamId ?? null) : null);
  const create = useCreateAllocation();
  const [pieceTouched, setPieceTouched] = useState(false);

  if (target === null) return <Modal open={false} />;
  const { progress } = target;
  const maxQty = handover ? progress.unallocatedQty : target.worker.retrievableQty;
  const pieceMax = handover ? target.pieceLimit : undefined;
  const required = [{ required: true, message: MESSAGES.common.required }];
  const workerOptions = (members.data ?? [])
    .filter((m) => m.employeeId !== user?.employeeId)
    .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }));

  const handleFinish = (values: FormValues) =>
    create.mutate(
      {
        movementType: handover ? MOVEMENT_TYPE.HANDOVER : MOVEMENT_TYPE.RETRIEVAL,
        workerId: handover ? (values.workerId as number) : target.worker.workerId,
        orderLineId: progress.orderLineId,
        operationId: progress.operationId,
        quantity: values.quantity as number,
        pieceQty: progress.usesSemiFinished ? values.pieceQty : undefined,
        retrievalReason: handover ? undefined : values.retrievalReason,
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open
      title={handover ? PM_LABELS.giveTitle : PM_LABELS.retrieveTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        disabled={create.isPending}
        initialValues={{ quantity: maxQty > 0 ? maxQty : undefined, pieceQty: handover ? Math.min(maxQty, pieceMax ?? 0) : maxQty }}
        onValuesChange={(changed: FormValues) => {
          // Công đoạn giao kèm BTP: số BTP điền sẵn theo số lần cho tới khi tổ trưởng tự sửa.
          if (changed.pieceQty !== undefined) setPieceTouched(true);
          if (changed.quantity !== undefined && !pieceTouched && progress.usesSemiFinished)
            form.setFieldsValue({
              pieceQty: handover ? Math.min(changed.quantity, pieceMax ?? 0) : changed.quantity,
            });
        }}
      >
        <Descriptions column={1} size="small" bordered style={{ marginBottom: 16 }}>
          <Descriptions.Item label={PM_LABELS.orderLine}>
            {`${progress.orderCode} · ${progress.productCode} ${progress.color}-${progress.sizeCode}`}
          </Descriptions.Item>
          <Descriptions.Item label={PM_LABELS.operation}>
            {`${progress.operationCode} - ${progress.operationName}`}
          </Descriptions.Item>
          {handover ? (
            <Descriptions.Item label={PM_LABELS.unallocated}>{progress.unallocatedQty}</Descriptions.Item>
          ) : (
            <Descriptions.Item label={PM_LABELS.worker}>
              {`${target.worker.workerCode} - ${target.worker.workerName} (${PM_LABELS.retrievable} ${target.worker.retrievableQty})`}
            </Descriptions.Item>
          )}
        </Descriptions>
        {handover && (
          <Form.Item name="workerId" label={PM_LABELS.worker} rules={required}>
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={PM_LABELS.selectWorker}
              options={workerOptions}
              loading={members.isLoading}
            />
          </Form.Item>
        )}
        {!handover && (
          <Form.Item name="retrievalReason" label={PM_LABELS.retrievalReason} rules={required}>
            <Select placeholder={PM_LABELS.selectReason} options={REASON_OPTIONS} />
          </Form.Item>
        )}
        <Form.Item name="quantity" label={PM_LABELS.quantity} rules={required}>
          <InputNumber min={1} max={maxQty} precision={0} style={{ width: 200 }} />
        </Form.Item>
        {progress.usesSemiFinished && (
          <Form.Item
            name="pieceQty"
            label={handover ? PM_LABELS.pieceQty : PM_LABELS.pieceQtyBack}
            extra={handover ? `${PM_LABELS.teamPieces}: còn giao kèm được ${pieceMax}` : undefined}
          >
            <InputNumber min={0} max={pieceMax} precision={0} style={{ width: 200 }} />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
