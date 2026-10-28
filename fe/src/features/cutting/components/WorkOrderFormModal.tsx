import { useEffect } from 'react';
import { Alert, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Table, Tag } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineOptions, useTeamOptions } from '@/hooks/useLookups';
import type { OrderLineOption } from '@/types/lookup';
import { CUTTING_TEAM_TYPE, LABELS } from '../constants';
import { useFabricPreview, useSaveWorkOrder } from '../hooks/useCutting';
import type { FabricPreviewRow, SaveWorkOrderPayload, WorkOrder } from '../types';
import { formatQty } from '@/utils/format';

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
  const cuttingTeamId = Form.useWatch('cuttingTeamId', form);
  const planned = Form.useWatch('planned', form);

  // Xem trước vải ngay khi nhập kế hoạch (không lưu gì): báo thiếu trước khi bấm Lưu.
  const previewLines = Object.entries(planned ?? {})
    .filter(([, qty]) => (qty ?? 0) > 0)
    .map(([orderLineId, qty]) => ({ orderLineId: Number(orderLineId), plannedQty: qty ?? 0 }));
  const previewBody =
    open && orderId && productId && cuttingTeamId && previewLines.length > 0
      ? { orderId, productId, cuttingTeamId, dueDate: dayjs().format(API_DATE), lines: previewLines }
      : null;
  const preview = useFabricPreview(previewBody, workOrder?.workOrderId);
  const fabric = preview.data;
  const blocked = previewBody !== null && fabric !== undefined && !fabric.enough;

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
      okButtonProps={{ disabled: blocked }}
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
        {previewBody && (
          <div style={{ marginTop: 16 }}>
            {fabric && fabric.missingNormLines.length > 0 && (
              <Alert
                type="error"
                showIcon
                message={LABELS.fabricMissingNorm.replace('{lines}', fabric.missingNormLines.join(', '))}
              />
            )}
            {fabric && fabric.missingNormLines.length === 0 && (
              <Alert
                type={fabric.enough ? 'success' : 'error'}
                showIcon
                message={fabric.enough ? LABELS.fabricEnough : LABELS.fabricShortAlert}
                description={LABELS.fabricPreviewHint}
              />
            )}
            <Table<FabricPreviewRow>
              rowKey="materialId"
              size="small"
              style={{ marginTop: 8 }}
              loading={preview.isFetching}
              pagination={false}
              dataSource={fabric?.rows ?? []}
              columns={[
                {
                  title: LABELS.fabricPreviewMaterial,
                  key: 'm',
                  render: (_, r) => `${r.materialCode} ${r.materialName}${r.materialColor ? ` (${r.materialColor})` : ''}`,
                },
                { title: LABELS.fabricNeed, dataIndex: 'requiredQty', align: 'right', render: (v: number, r) => `${formatQty(v)} ${r.unit}` },
                { title: LABELS.fabricWarehouse, dataIndex: 'warehouseAvailable', align: 'right', render: (v: number) => formatQty(v) },
                { title: LABELS.fabricReserved, dataIndex: 'reservedByOtherWorkOrders', align: 'right', render: (v: number) => formatQty(v) },
                { title: LABELS.fabricTeamHolding, dataIndex: 'teamHolding', align: 'right', render: (v: number) => formatQty(v) },
                { title: LABELS.fabricAvailable, dataIndex: 'availableQty', align: 'right', render: (v: number) => formatQty(v) },
                {
                  title: LABELS.fabricShort,
                  dataIndex: 'shortQty',
                  align: 'right',
                  render: (v: number) => (v > 0 ? <Tag color="red">{formatQty(v)}</Tag> : <Tag color="green">0</Tag>),
                },
              ]}
            />
          </div>
        )}
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
