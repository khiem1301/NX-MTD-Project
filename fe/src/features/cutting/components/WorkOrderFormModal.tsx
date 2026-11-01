import { useEffect } from 'react';
import { Col, Form, Input, Modal, Row, Select, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineOptions, useOrderOptions, useTeamOptions } from '@/hooks/useLookups';
import { CUTTING_TEAM_TYPE, LABELS, WORK_ORDER_LIMITS } from '../constants';
import { useSaveWorkOrder, useWorkOrderFabricCheck } from '../hooks/useCutting';
import type { SaveWorkOrderPayload, WorkOrder } from '../types';
import {
  hasDuplicateLines,
  splitWorkOrderLines,
  toWorkOrderLines,
  type ExtraComponentInput,
} from '../utils/workOrderLines';
import { ExtraComponentLines } from './ExtraComponentLines';
import { PlannedLinesTable } from './PlannedLinesTable';
import { FabricCheckAlerts } from './FabricCheckAlerts';
import { API_DATE } from '@/utils/format';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';

type WorkOrderFormModalProps = { open: boolean; workOrder: WorkOrder | null; onClose: () => void };

type FormValues = Omit<SaveWorkOrderPayload, 'dueDate' | 'lines'> & {
  dueDate: Dayjs;
  /** Số kế hoạch theo từng dòng đơn; 0 / trống = không cắt dòng đó. */
  planned: Record<string, number | null>;
  extras: ExtraComponentInput[];
};

const FIELD_STYLE = { marginBottom: 12 };

/** UC19 (tổ cắt): Quản đốc lập lệnh cắt theo dòng đơn của một mã hàng. */
export function WorkOrderFormModal({ open, workOrder, onClose }: WorkOrderFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveWorkOrder();
  const orderId = Form.useWatch('orderId', form);
  const productId = Form.useWatch('productId', form);
  const cuttingTeamId = Form.useWatch('cuttingTeamId', form);
  const planned = Form.useWatch('planned', form);
  const extras = Form.useWatch('extras', form);
  const orderLines = useOrderLineOptions(orderId);
  const teams = useTeamOptions(CUTTING_TEAM_TYPE);
  const orders = useOrderOptions();
  // Hạn lệnh cắt phải sau ngày bắt đầu (ngày nhận) của đơn.
  const startDate = orders.data?.find((o) => o.orderId === orderId)?.receivedDate;
  const checkLines = toWorkOrderLines(planned, extras);
  const fabricChecks = useWorkOrderFabricCheck(
    open && orderId && cuttingTeamId && checkLines.length > 0
      ? { orderId, cuttingTeamId, lines: checkLines }
      : null,
  );

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (!workOrder) return;
    form.setFieldsValue({
      orderId: workOrder.orderId,
      productId: workOrder.productId,
      cuttingTeamId: workOrder.cuttingTeamId,
      dueDate: dayjs(workOrder.dueDate),
      note: workOrder.note ?? undefined,
      ...splitWorkOrderLines(workOrder.lines),
    });
  }, [open, workOrder, form]);

  const products = [...new Map((orderLines.data ?? []).map((l) => [l.productId, l])).values()];
  const productLines = (orderLines.data ?? []).filter((l) => l.productId === productId);

  const handleFinish = ({
    planned: plannedQty,
    extras: extraLines,
    dueDate,
    ...rest
  }: FormValues) => {
    const lines = toWorkOrderLines(plannedQty, extraLines);
    if (lines.length === 0) return void message.warning(LABELS.noLines);
    if (hasDuplicateLines(lines)) return void message.warning(LABELS.duplicateExtra);
    const body: SaveWorkOrderPayload = { ...rest, dueDate: dueDate.format(API_DATE), lines };
    save.mutate({ id: workOrder?.workOrderId, body }, { onSuccess: onClose });
  };
  const required = [{ required: true, message: MESSAGES.common.required }];
  // Sửa lệnh cũ mà giữ nguyên hạn đã qua thì vẫn lưu được (BE cũng không kiểm lại hạn không đổi).
  const keepsSavedDue = (d: Dayjs) => !!workOrder && d.isSame(dayjs(workOrder.dueDate), 'day');

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      centered
      title={
        workOrder ? `${MESSAGES.common.edit} ${workOrder.workOrderCode}` : LABELS.createWorkOrder
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
          <Col xs={24} sm={12} lg={8}>
            <Form.Item name="orderId" label={LABELS.order} rules={required} style={FIELD_STYLE}>
              <OrderSelect
                width="100%"
                placeholder={LABELS.selectOrder}
                onChange={() =>
                  form.setFieldsValue({ productId: undefined, planned: {}, extras: [] })
                }
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} lg={7}>
            <Form.Item name="productId" label={LABELS.product} rules={required} style={FIELD_STYLE}>
              <Select
                placeholder={LABELS.selectProduct}
                disabled={!orderId}
                options={products.map((p) => ({
                  value: p.productId,
                  label: `${p.productCode} ${p.productName}`,
                }))}
                onChange={() => form.setFieldsValue({ planned: {}, extras: [] })}
              />
            </Form.Item>
          </Col>
          <Col xs={12} lg={5}>
            <Form.Item
              name="cuttingTeamId"
              label={LABELS.cuttingTeam}
              rules={required}
              style={FIELD_STYLE}
            >
              <Select
                placeholder={LABELS.selectCuttingTeam}
                options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))}
              />
            </Form.Item>
          </Col>
          <Col xs={12} lg={4}>
            <Form.Item
              name="dueDate"
              label={LABELS.dueDate}
              dependencies={['orderId']}
              style={FIELD_STYLE}
              tooltip={
                startDate
                  ? `Phải sau ngày bắt đầu của đơn (${dayjs(startDate).format('DD/MM/YYYY')})`
                  : undefined
              }
              rules={[
                ...required,
                {
                  validator: (_, value: Dayjs | undefined) =>
                    value?.isBefore(dayjs(), 'day') && !keepsSavedDue(value)
                      ? Promise.reject(new Error('Không đặt hạn trong quá khứ.'))
                      : !value || !startDate || value.isAfter(dayjs(startDate), 'day')
                        ? Promise.resolve()
                        : Promise.reject(
                            new Error(
                              `Hạn phải sau ngày bắt đầu của đơn (${dayjs(startDate).format('DD/MM/YYYY')}).`,
                            ),
                          ),
                },
              ]}
            >
              <AppDatePicker
                style={{ width: '100%' }}
                disabledDate={(d) =>
                  (d.isBefore(dayjs(), 'day') && !keepsSavedDue(d)) ||
                  (!!startDate && !d.isAfter(dayjs(startDate), 'day'))
                }
              />
            </Form.Item>
          </Col>
        </Row>
        <PlannedLinesTable lines={productLines} />
        <ExtraComponentLines form={form} lines={productLines} />
        <FabricCheckAlerts checks={fabricChecks} />
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 12, marginBottom: 0 }}>
          <Input.TextArea
            autoSize={{ minRows: 1, maxRows: 3 }}
            maxLength={WORK_ORDER_LIMITS.noteMax}
            showCount
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
