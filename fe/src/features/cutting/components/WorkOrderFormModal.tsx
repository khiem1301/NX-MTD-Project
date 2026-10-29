import { useEffect } from 'react';
import { DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineOptions, useTeamOptions } from '@/hooks/useLookups';
import type { OrderLineOption } from '@/types/lookup';
import { CUTTING_TEAM_TYPE, LABELS } from '../constants';
import { useSaveWorkOrder } from '../hooks/useCutting';
import type { SaveWorkOrderPayload, WorkOrder } from '../types';

type WorkOrderFormModalProps = { open: boolean; workOrder: WorkOrder | null; onClose: () => void };

type FormValues = Omit<SaveWorkOrderPayload, 'dueDate' | 'lines'> & {
  dueDate: Dayjs;
  /** Số kế hoạch theo từng dòng đơn; 0 / trống = không cắt dòng đó. */
  planned: Record<string, number | null>;
};

const DATE_FORMAT = 'DD/MM/YYYY';
const API_DATE = 'YYYY-MM-DD';

/** UC19 (tổ cắt): Quản đốc lập lệnh cắt theo dòng đơn của một mã hàng. */
export function WorkOrderFormModal({ open, workOrder, onClose }: WorkOrderFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveWorkOrder();
  const orderId = Form.useWatch('orderId', form);
  const productId = Form.useWatch('productId', form);
  const orderLines = useOrderLineOptions(orderId);
  const teams = useTeamOptions(CUTTING_TEAM_TYPE);

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
      planned: Object.fromEntries(
        workOrder.lines.map((l) => [String(l.orderLineId), l.plannedQty]),
      ),
    });
  }, [open, workOrder, form]);

  const products = [...new Map((orderLines.data ?? []).map((l) => [l.productId, l])).values()];
  const productLines = (orderLines.data ?? []).filter((l) => l.productId === productId);

  const handleFinish = ({ planned, dueDate, ...rest }: FormValues) => {
    const lines = Object.entries(planned ?? {})
      .filter(([, qty]) => (qty ?? 0) > 0)
      .map(([orderLineId, qty]) => ({ orderLineId: Number(orderLineId), plannedQty: qty ?? 0 }));
    const body: SaveWorkOrderPayload = { ...rest, dueDate: dueDate.format(API_DATE), lines };
    save.mutate({ id: workOrder?.workOrderId, body }, { onSuccess: onClose });
  };
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={860}
      title={
        workOrder ? `${MESSAGES.common.edit} ${workOrder.workOrderCode}` : LABELS.workOrdersTitle
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
          <Form.Item name="orderId" label={LABELS.order} rules={required}>
            <OrderSelect
              onChange={() => form.setFieldsValue({ productId: undefined, planned: {} })}
            />
          </Form.Item>
          <Form.Item name="productId" label={LABELS.product} rules={required}>
            <Select
              style={{ width: 180 }}
              options={products.map((p) => ({
                value: p.productId,
                label: `${p.productCode} ${p.productName}`,
              }))}
              onChange={() => form.setFieldValue('planned', {})}
            />
          </Form.Item>
          <Form.Item name="cuttingTeamId" label={LABELS.cuttingTeam} rules={required}>
            <Select
              style={{ width: 180 }}
              options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))}
            />
          </Form.Item>
          <Form.Item name="dueDate" label={LABELS.dueDate} rules={required}>
            <DatePicker format={DATE_FORMAT} />
          </Form.Item>
        </Space>
        <Table<OrderLineOption>
          rowKey="orderLineId"
          size="small"
          pagination={false}
          dataSource={productLines}
          columns={[
            {
              title: LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.color} - ${l.sizeCode}`,
            },
            { title: LABELS.orderedQty, dataIndex: 'orderedQty', align: 'right' },
            {
              title: LABELS.plannedQty,
              key: 'planned',
              render: (_, l) => (
                <Form.Item name={['planned', String(l.orderLineId)]} noStyle>
                  <InputNumber min={0} max={l.orderedQty} precision={0} />
                </Form.Item>
              ),
            },
          ]}
        />
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
