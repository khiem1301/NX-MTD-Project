import { useEffect } from 'react';
import { Col, Form, Input, Modal, Row, Select, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import {
  LABELS,
  LAY_FABRIC_DECIMALS,
  LAY_MAX_FABRIC_USED_M,
  SHIFT_OPTIONS,
  lineKey,
  parseLineKey,
} from '../constants';
import { useSaveLay } from '../hooks/useCutting';
import type { CuttingLay, SaveCuttingLayPayload, WorkOrder } from '../types';
import { teamFabricBalance } from '../utils/layRemaining';
import { LayFabricBalance } from './LayFabricBalance';
import { LayLinesTable } from './LayLinesTable';
import { API_DATE, fill, formatQty } from '@/utils/format';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';
import { QtyInput } from '@/components/common/QtyInput';

const FULL = { width: '100%' };

type LayFormModalProps = {
  open: boolean;
  workOrder: WorkOrder;
  lay: CuttingLay | null;
  onClose: () => void;
};

type FormValues = Omit<SaveCuttingLayPayload, 'workDate' | 'lines' | 'workOrderId'> & {
  workDate: Dayjs;
  qty: Record<string, number | null>;
};

/**
 * UC51: nhập bàn cắt của lệnh cắt: số lượng sau cắt từng màu - size và số mét vải đã dùng của cả bàn (DEV-ALL-90).
 * Hiện vải còn lại của tổ trên đơn; số mét vượt số còn lại thì báo ngay (BE cũng chặn), BE chặn vượt kế hoạch + % dư.
 */
export function LayFormModal({ open, workOrder, lay, onClose }: LayFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveLay();
  const fabricId = Form.useWatch('fabricMaterialId', form);
  const usedM = (Form.useWatch('fabricUsedM', form) as number | null | undefined) ?? 0;
  const fabric = workOrder.fabrics.find((f) => f.materialId === fabricId);
  const balance = fabric ? teamFabricBalance(fabric, lay, usedM) : null;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      lay
        ? {
            workDate: dayjs(lay.workDate),
            shiftCode: lay.shiftCode,
            fabricMaterialId: lay.fabricMaterialId ?? undefined,
            fabricUsedM: lay.fabricUsedM ?? undefined,
            note: lay.note ?? undefined,
            qty: Object.fromEntries(lay.lines.map((l) => [lineKey(l), l.actualQty])),
          }
        : {
            workDate: dayjs(),
            shiftCode: SHIFT_OPTIONS[0]?.value,
            fabricMaterialId: workOrder.fabrics[0]?.materialId,
          },
    );
  }, [open, lay, workOrder, form]);

  const handleFinish = ({ qty, workDate, ...rest }: FormValues) => {
    const lines = Object.entries(qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ ...parseLineKey(id), actualQty: q ?? 0 }));
    if (lines.length === 0) return void message.warning(MESSAGES.cutting.layQtyRequired);
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
  const withinRemaining = async (_rule: unknown, value?: number | null) => {
    if (balance && (value ?? 0) > balance.left)
      throw new Error(fill(MESSAGES.cutting.layFabricOverRemaining, formatQty(balance.left)));
  };

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
          <Col xs={12} md={5}>
            <Form.Item name="workDate" label={LABELS.workDate} rules={required}>
              <AppDatePicker
                style={FULL}
                disabledDate={(d) =>
                  d.isAfter(dayjs(), 'day') || d.isBefore(dayjs(workOrder.createdAt), 'day')
                }
              />
            </Form.Item>
          </Col>
          <Col xs={12} md={4}>
            <Form.Item name="shiftCode" label={LABELS.shift} rules={required}>
              <Select options={SHIFT_OPTIONS} />
            </Form.Item>
          </Col>
          <Col xs={24} md={9}>
            <Form.Item name="fabricMaterialId" label={LABELS.fabricMaterial} rules={required}>
              <Select options={fabricOptions} />
            </Form.Item>
          </Col>
          <Col xs={24} md={6}>
            <Form.Item
              name="fabricUsedM"
              label={LABELS.layFabricUsed}
              dependencies={['fabricMaterialId']}
              rules={[...required, { validator: withinRemaining }]}
            >
              <QtyInput
                min={0.01}
                max={LAY_MAX_FABRIC_USED_M}
                decimals={LAY_FABRIC_DECIMALS}
                style={FULL}
              />
            </Form.Item>
          </Col>
        </Row>
        {balance && <LayFabricBalance balance={balance} />}
        <LayLinesTable lines={workOrder.lines} editingLines={lay?.lines} />
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16, marginBottom: 0 }}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
