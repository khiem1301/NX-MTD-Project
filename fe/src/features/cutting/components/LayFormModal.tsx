import { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { LABELS, SHIFT_OPTIONS, lineKey, parseLineKey } from '../constants';
import { useSaveLay } from '../hooks/useCutting';
import type { CuttingLay, SaveCuttingLayPayload, WorkOrder } from '../types';
import { LayLinesTable } from './LayLinesTable';
import { API_DATE } from '@/utils/format';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';

type LayFormModalProps = {
  open: boolean;
  workOrder: WorkOrder;
  lay: CuttingLay | null;
  onClose: () => void;
};

type FormValues = Omit<SaveCuttingLayPayload, 'workDate' | 'lines' | 'workOrderId'> & {
  workDate: Dayjs;
  ratio: Record<string, number | null>;
  actual: Record<string, number | null>;
  reason: Record<string, string | undefined>;
};

/** UC51: nhập bàn cắt của lệnh cắt (số lớp × tỉ lệ size); BE chặn vượt kế hoạch + % dư. */
export function LayFormModal({ open, workOrder, lay, onClose }: LayFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveLay();

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    const firstFabric = workOrder.fabrics[0];
    form.setFieldsValue(
      lay
        ? {
            workDate: dayjs(lay.workDate),
            shiftCode: lay.shiftCode,
            fabricMaterialId: lay.fabricMaterialId ?? undefined,
            plyCount: lay.plyCount,
            note: lay.note ?? undefined,
            ratio: Object.fromEntries(lay.lines.map((l) => [lineKey(l), l.ratioValue])),
            actual: Object.fromEntries(
              lay.lines
                .filter((l) => l.actualQty !== l.calculatedQty)
                .map((l) => [lineKey(l), l.actualQty]),
            ),
            reason: Object.fromEntries(
              lay.lines.map((l) => [lineKey(l), l.adjustReason ?? undefined]),
            ),
          }
        : {
            workDate: dayjs(),
            shiftCode: SHIFT_OPTIONS[0]?.value,
            fabricMaterialId: firstFabric?.materialId,
          },
    );
  }, [open, lay, workOrder, form]);

  const handleFinish = ({ ratio, actual, reason, workDate, ...rest }: FormValues) => {
    const lines = Object.entries(ratio ?? {})
      .filter(([, r]) => (r ?? 0) > 0)
      .map(([id, r]) => ({
        ...parseLineKey(id),
        ratioValue: r ?? 0,
        actualQty: actual?.[id] ?? undefined,
        adjustReason: reason?.[id]?.trim() || undefined,
      }));
    const body: SaveCuttingLayPayload = {
      ...rest,
      workOrderId: workOrder.workOrderId,
      workDate: workDate.format(API_DATE),
      lines,
    };
    save.mutate({ id: lay?.cuttingLayId, body }, { onSuccess: onClose });
  };

  const fabricOptions = workOrder.fabrics.map((f) => ({
    value: f.materialId,
    label: `${f.materialCode} ${f.materialName}${f.materialColor ? ` (${f.materialColor})` : ''}`,
  }));
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={
        lay
          ? `${LABELS.laySequence} ${lay.laySequence}`
          : `${LABELS.laysTitle} · ${workOrder.workOrderCode}`
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Space wrap align="start">
          <Form.Item name="workDate" label={LABELS.workDate} rules={required}>
            <AppDatePicker />
          </Form.Item>
          <Form.Item name="shiftCode" label={LABELS.shift} rules={required}>
            <Select options={SHIFT_OPTIONS} style={{ width: 130 }} />
          </Form.Item>
          <Form.Item name="fabricMaterialId" label={LABELS.fabricMaterial} rules={required}>
            <Select options={fabricOptions} style={{ width: 320 }} />
          </Form.Item>
          <Form.Item name="plyCount" label={LABELS.plyCount} rules={required}>
            <InputNumber min={1} precision={0} style={{ width: 110 }} />
          </Form.Item>
        </Space>
        <LayLinesTable lines={workOrder.lines} />
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
