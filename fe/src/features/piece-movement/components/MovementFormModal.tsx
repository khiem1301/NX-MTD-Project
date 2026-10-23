import { useMemo, useState } from 'react';
import { Alert, Form, InputNumber, Modal, Radio, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useAuth } from '@/hooks/useAuth';
import {
  MOVEMENT_TYPE,
  MOVEMENT_TYPE_LABEL,
  PM_LABELS,
  RETRIEVAL_REASON_LABEL,
} from '../constants';
import { useCreateMovement, useHoldings } from '../hooks/usePieceMovement';

type MovementFormModalProps = { open: boolean; onClose: () => void };
type FormValues = {
  movementType: string;
  workerId?: number;
  orderLineId?: number;
  quantity?: number;
  retrievalReason?: string;
};

const TYPE_OPTIONS = Object.values(MOVEMENT_TYPE).map((value) => ({
  value,
  label: MOVEMENT_TYPE_LABEL[value],
}));
const REASON_OPTIONS = Object.entries(RETRIEVAL_REASON_LABEL).map(([value, label]) => ({
  value,
  label,
}));

/**
 * UC53: tổ trưởng ghi giao BTP (từ số tổ đang giữ) hoặc thu hồi BTP (từ số công nhân đang giữ, kèm lý do).
 * Số lượng bị chặn theo số còn giao / thu hồi được; công nhân xác nhận sau đó (UC60).
 */
export function MovementFormModal({ open, onClose }: MovementFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const { user } = useAuth();
  const members = useTeamMemberLookup(open ? (user?.teamId ?? null) : null);
  const holdings = useHoldings(open);
  const create = useCreateMovement();
  const [type, setType] = useState<string>(MOVEMENT_TYPE.HANDOVER);
  const [workerId, setWorkerId] = useState<number>();
  const [lineId, setLineId] = useState<number>();
  const isHandover = type === MOVEMENT_TYPE.HANDOVER;

  const lineOptions = useMemo(() => {
    if (isHandover)
      return (holdings.data?.team ?? [])
        .filter((l) => l.availableQty > 0)
        .map((l) => ({
          value: l.orderLineId,
          max: l.availableQty,
          label: `${l.orderCode} · ${l.productCode} ${l.color}-${l.sizeCode} · ${PM_LABELS.available} ${l.availableQty}`,
        }));
    return (holdings.data?.workers ?? [])
      .filter((w) => w.workerId === workerId && w.retrievableQty > 0)
      .map((w) => ({
        value: w.orderLineId,
        max: w.retrievableQty,
        label: `${w.orderCode} · ${w.productCode} ${w.color}-${w.sizeCode} · ${PM_LABELS.retrievable} ${w.retrievableQty}`,
      }));
  }, [holdings.data, isHandover, workerId]);
  const max = lineOptions.find((o) => o.value === lineId)?.max;

  const workerOptions = (members.data ?? [])
    .filter((m) => m.employeeId !== user?.employeeId)
    .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }));
  const required = [{ required: true, message: MESSAGES.common.required }];

  const handleFinish = (values: FormValues) =>
    create.mutate(
      {
        movementType: values.movementType,
        workerId: values.workerId as number,
        orderLineId: values.orderLineId as number,
        quantity: values.quantity as number,
        retrievalReason: isHandover ? undefined : values.retrievalReason,
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open={open}
      title={PM_LABELS.create}
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
        initialValues={{ movementType: MOVEMENT_TYPE.HANDOVER }}
      >
        <Form.Item name="movementType" label={PM_LABELS.type} rules={required}>
          <Radio.Group
            optionType="button"
            options={TYPE_OPTIONS}
            onChange={(e) => {
              setType(e.target.value as string);
              setLineId(undefined);
              form.setFieldsValue({ orderLineId: undefined, quantity: undefined });
            }}
          />
        </Form.Item>
        <Form.Item name="workerId" label={PM_LABELS.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={PM_LABELS.selectWorker}
            options={workerOptions}
            loading={members.isLoading}
            onChange={(id: number) => {
              setWorkerId(id);
              setLineId(undefined);
              form.setFieldsValue({ orderLineId: undefined, quantity: undefined });
            }}
          />
        </Form.Item>
        {!holdings.isLoading &&
          lineOptions.length === 0 &&
          (isHandover || workerId !== undefined) && (
            <Alert
              type="info"
              showIcon
              message={PM_LABELS.noHolding}
              style={{ marginBottom: 12 }}
            />
          )}
        <Form.Item name="orderLineId" label={PM_LABELS.orderLine} rules={required}>
          <Select
            placeholder={PM_LABELS.selectLine}
            options={lineOptions}
            loading={holdings.isLoading}
            onChange={(id: number) => setLineId(id)}
          />
        </Form.Item>
        {!isHandover && (
          <Form.Item name="retrievalReason" label={PM_LABELS.retrievalReason} rules={required}>
            <Select placeholder={PM_LABELS.selectReason} options={REASON_OPTIONS} />
          </Form.Item>
        )}
        <Form.Item name="quantity" label={PM_LABELS.quantity} rules={required}>
          <InputNumber min={1} max={max} precision={0} style={{ width: 200 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
