import { REASON_MAX_LENGTH } from '@/constants/limits';
import { Descriptions, Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { QtyInput } from '@/components/common/QtyInput';
import { maxQtyRule } from '@/utils/qtyLimit';
import { MOVEMENT_TYPE_LABEL, PM_LABELS, PM_MESSAGES } from '../constants';
import { useConfirmByWorker, useConfirmOnBehalf } from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';

type ConfirmMovementModalProps = {
  movement: WorkAllocation | null;
  /** true = tổ trưởng xác nhận hộ (bắt buộc lý do); false = công nhân tự xác nhận. */
  onBehalf: boolean;
  onClose: () => void;
};

type FormValues = {
  confirmedQty: number;
  discrepancyReason?: string;
  onBehalfReason?: string;
};

/** UC60 / UC54: đếm số lần được giao thực nhận (từ 1 đến số trên phiếu, lệch phải có lý do); tổ trưởng xác nhận hộ phải ghi lý do. */
export function ConfirmMovementModal({ movement, onBehalf, onClose }: ConfirmMovementModalProps) {
  const [form] = Form.useForm<FormValues>();
  const byWorker = useConfirmByWorker();
  const behalf = useConfirmOnBehalf();
  const loading = byWorker.isPending || behalf.isPending;

  const handleFinish = (values: FormValues) => {
    if (!movement) return;
    const base = {
      confirmedQty: values.confirmedQty,
      discrepancyReason: values.discrepancyReason?.trim() || undefined,
    };
    if (!onBehalf) {
      byWorker.mutate({ id: movement.workAllocationId, body: base }, { onSuccess: onClose });
      return;
    }
    // Xác nhận hộ là ngoại lệ (công nhân lẽ ra tự xác nhận): cảnh báo trước khi gửi.
    Modal.confirm({
      title: PM_LABELS.onBehalfWarnTitle,
      content: PM_LABELS.onBehalfWarnContent,
      okText: PM_LABELS.onBehalfWarnOk,
      cancelText: MESSAGES.common.cancel,
      okButtonProps: { danger: true },
      onOk: () =>
        behalf.mutate(
          {
            id: movement.workAllocationId,
            body: { ...base, onBehalfReason: (values.onBehalfReason ?? '').trim() },
          },
          { onSuccess: onClose },
        ),
    });
  };

  return (
    <Modal
      open={movement !== null}
      title={onBehalf ? PM_LABELS.confirmOnBehalf : PM_LABELS.confirm}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={loading}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {movement && (
        <Form
          form={form}
          layout="vertical"
          disabled={loading}
          initialValues={{ confirmedQty: movement.quantity }}
          onFinish={handleFinish}
        >
          <Descriptions column={1} size="small" bordered style={{ marginBottom: 16 }}>
            <Descriptions.Item label={PM_LABELS.type}>
              {MOVEMENT_TYPE_LABEL[movement.movementType]}
            </Descriptions.Item>
            <Descriptions.Item
              label={PM_LABELS.worker}
            >{`${movement.workerCode} - ${movement.workerName}`}</Descriptions.Item>
            <Descriptions.Item label={PM_LABELS.orderLine}>
              {`${movement.orderCode} · ${movement.productCode} ${movement.color}-${movement.sizeCode}`}
            </Descriptions.Item>
            <Descriptions.Item label={PM_LABELS.operation}>
              {`${movement.operationCode} - ${movement.operationName}`}
            </Descriptions.Item>
            <Descriptions.Item label={PM_LABELS.quantity}>{movement.quantity}</Descriptions.Item>
          </Descriptions>
          <Form.Item
            name="confirmedQty"
            label={PM_LABELS.confirmedQty}
            rules={[
              { required: true, message: MESSAGES.common.required },
              maxQtyRule(movement.quantity),
            ]}
          >
            <QtyInput min={1} />
          </Form.Item>
          <Form.Item
            name="discrepancyReason"
            label={PM_LABELS.discrepancyReason}
            dependencies={['confirmedQty']}
            rules={[
              {
                validator: async (_rule, value?: string) => {
                  const qty = form.getFieldValue('confirmedQty') as number | undefined;
                  if (qty !== movement.quantity && !value?.trim())
                    throw new Error(PM_MESSAGES.discrepancyReasonRequired);
                },
              },
            ]}
          >
            <Input maxLength={REASON_MAX_LENGTH} />
          </Form.Item>
          {onBehalf && (
            <Form.Item
              name="onBehalfReason"
              label={PM_LABELS.onBehalfReason}
              rules={[
                { required: true, whitespace: true, message: MESSAGES.common.reasonRequired },
              ]}
            >
              <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
            </Form.Item>
          )}
        </Form>
      )}
    </Modal>
  );
}
