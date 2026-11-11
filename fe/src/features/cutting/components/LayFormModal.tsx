import { useEffect } from 'react';
import { Col, Form, Input, Modal, Row, Select, Typography, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { LABELS, SHIFT_OPTIONS, lineKey, parseLineKey } from '../constants';
import { useSaveLay } from '../hooks/useCutting';
import type { CuttingLay, SaveCuttingLayPayload, WorkOrder } from '../types';
import { requiredFabricIds } from '../utils/layRemaining';
import { LayFabricBalance } from './LayFabricBalance';
import { LayLinesTable } from './LayLinesTable';
import { API_DATE } from '@/utils/format';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';

const FULL = { width: '100%' };

type LayFormModalProps = {
  open: boolean;
  workOrder: WorkOrder;
  lay: CuttingLay | null;
  onClose: () => void;
};

type FormValues = Pick<SaveCuttingLayPayload, 'shiftCode' | 'note'> & {
  workDate: Dayjs;
  qty: Record<string, number | null>;
  fabricUsed: Record<string, number | null>;
};

/**
 * UC51: nhập bàn cắt của lệnh cắt: số lượng sau cắt từng màu - size và số mét đã dùng của từng vải (DEV-ALL-90, DEV-ALL-93).
 * Vải phải nhập = hợp vải các dòng có số lượng (cả bộ: mọi vải của bộ). Vượt vải còn lại của tổ thì báo ngay (BE cũng chặn).
 */
export function LayFormModal({ open, workOrder, lay, onClose }: LayFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveLay();
  const qty = (Form.useWatch('qty', form) as FormValues['qty'] | undefined) ?? {};
  const fabricIds = requiredFabricIds(workOrder.lines, qty);
  const fabrics = fabricIds.flatMap((id) => workOrder.fabrics.filter((f) => f.materialId === id));

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      lay
        ? {
            workDate: dayjs(lay.workDate),
            shiftCode: lay.shiftCode,
            note: lay.note ?? undefined,
            qty: Object.fromEntries(lay.lines.map((l) => [lineKey(l), l.actualQty])),
            fabricUsed: Object.fromEntries(lay.fabrics.map((f) => [String(f.materialId), f.usedM])),
          }
        : { workDate: dayjs(), shiftCode: SHIFT_OPTIONS[0]?.value },
    );
  }, [open, lay, workOrder, form]);

  const handleFinish = ({ qty: lineQty, fabricUsed, workDate, ...rest }: FormValues) => {
    const lines = Object.entries(lineQty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ ...parseLineKey(id), actualQty: q ?? 0 }));
    if (lines.length === 0) return void message.warning(MESSAGES.cutting.layQtyRequired);
    const body: SaveCuttingLayPayload = {
      ...rest,
      workOrderId: workOrder.workOrderId,
      workDate: workDate.format(API_DATE),
      fabrics: fabricIds.map((id) => ({ materialId: id, usedM: fabricUsed?.[String(id)] ?? 0 })),
      lines,
    };
    save.mutate({ id: lay?.cuttingLayId, body }, { onSuccess: onClose });
  };

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
        <Row gutter={12}>
          <Col xs={12} md={6}>
            <Form.Item name="workDate" label={LABELS.workDate} rules={required}>
              <AppDatePicker
                style={FULL}
                disabledDate={(d) =>
                  d.isAfter(dayjs(), 'day') || d.isBefore(dayjs(workOrder.createdAt), 'day')
                }
              />
            </Form.Item>
          </Col>
          <Col xs={12} md={5}>
            <Form.Item name="shiftCode" label={LABELS.shift} rules={required}>
              <Select options={SHIFT_OPTIONS} />
            </Form.Item>
          </Col>
        </Row>
        <LayLinesTable lines={workOrder.lines} editingLines={lay?.lines} />
        <Typography.Title level={5} style={{ marginTop: 16 }}>
          {LABELS.layFabrics}
        </Typography.Title>
        <LayFabricBalance fabrics={fabrics} lay={lay} />
        <Form.Item name="note" label={LABELS.note} style={{ marginBottom: 0 }}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
