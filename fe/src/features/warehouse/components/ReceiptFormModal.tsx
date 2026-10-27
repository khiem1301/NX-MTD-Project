import { useEffect } from 'react';
import { Button, DatePicker, Form, Input, InputNumber, Modal, Radio, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { formatQty } from '@/utils/format';
import { LABELS, MATERIAL_SOURCE, SOURCE_LABEL } from '../constants';
import { useOrderMaterials } from '../hooks/useOrderMaterials';
import { usePurchaseOrder, usePurchaseOrders } from '../hooks/usePurchaseOrders';
import { useSaveReceipt } from '../hooks/useReceipts';
import type { MaterialReceipt, ReceiptPreset, SaveReceiptPayload } from '../types';

type ReceiptFormModalProps = {
  open: boolean;
  receipt: MaterialReceipt | null;
  /** Điền sẵn khi lập từ màn NVL chờ nhận (bỏ qua khi sửa phiếu). */
  preset?: ReceiptPreset | null;
  onClose: () => void;
};

type FormValues = Omit<SaveReceiptPayload, 'receiptDate'> & { receiptDate: Dayjs };

const DATE_FORMAT = 'DD/MM/YYYY';
const API_DATE = 'YYYY-MM-DD';
const EMPTY_LINE = { acceptedQty: 0 };
const SOURCE_OPTIONS = [MATERIAL_SOURCE.CUSTOMER, MATERIAL_SOURCE.SUPPLIER].map((value) => ({
  value,
  label: SOURCE_LABEL[value],
}));

/**
 * UC37: Thủ kho lập / sửa phiếu nhập NVL. Khách cấp: chọn đơn hàng rồi NVL của đơn. Nhà cung cấp (TH-12): chọn đơn mua đã đặt,
 * mỗi dòng là một dòng đơn mua (đơn giá lấy từ đơn mua), số nhập không vượt số còn phải nhận. Thủ kho chỉ nhập số đạt: hàng thiếu
 * do hệ thống tự tính khi nhận đủ số lần giao dự kiến.
 */
export function ReceiptFormModal({ open, receipt, preset = null, onClose }: ReceiptFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveReceipt();
  const source = Form.useWatch('source', form) ?? MATERIAL_SOURCE.CUSTOMER;
  const orderId = Form.useWatch('orderId', form);
  const purchaseOrderId = Form.useWatch('purchaseOrderId', form);
  const isSupplier = source === MATERIAL_SOURCE.SUPPLIER;
  const orderMaterials = useOrderMaterials(isSupplier ? undefined : orderId);
  const purchaseOrders = usePurchaseOrders({ receivable: true, page: 1, pageSize: MAX_PAGE_SIZE });
  const purchaseOrder = usePurchaseOrder(isSupplier ? purchaseOrderId : undefined);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      receipt
        ? {
            source: receipt.source,
            orderId: receipt.orderId ?? undefined,
            purchaseOrderId: receipt.purchaseOrderId ?? undefined,
            note: receipt.note ?? undefined,
            receiptDate: dayjs(receipt.receiptDate),
            lines: receipt.lines.map((l) => ({
              orderMaterialId: l.orderMaterialId ?? undefined,
              purchaseOrderLineId: l.purchaseOrderLineId ?? undefined,
              acceptedQty: l.acceptedQty,
            })),
          }
        : preset
          ? {
              source: preset.source,
              orderId: preset.orderId,
              purchaseOrderId: preset.purchaseOrderId,
              receiptDate: dayjs(),
              lines: [
                {
                  orderMaterialId: preset.orderMaterialId,
                  purchaseOrderLineId: preset.purchaseOrderLineId,
                  acceptedQty: preset.acceptedQty,
                },
              ],
            }
          : { source: MATERIAL_SOURCE.CUSTOMER, receiptDate: dayjs(), lines: [EMPTY_LINE] },
    );
  }, [open, receipt, preset, form]);

  const resetLines = () => form.setFieldValue('lines', [EMPTY_LINE]);

  const handleFinish = (values: FormValues) => {
    const supplier = values.source === MATERIAL_SOURCE.SUPPLIER;
    const body: SaveReceiptPayload = {
      source: values.source,
      orderId: supplier ? undefined : values.orderId,
      purchaseOrderId: supplier ? values.purchaseOrderId : undefined,
      note: values.note,
      receiptDate: values.receiptDate.format(API_DATE),
      lines: values.lines.map((l) => ({
        orderMaterialId: supplier ? undefined : l.orderMaterialId,
        purchaseOrderLineId: supplier ? l.purchaseOrderLineId : undefined,
        acceptedQty: l.acceptedQty,
      })),
    };
    save.mutate({ id: receipt?.materialReceiptId, body }, { onSuccess: onClose });
  };

  const customerOptions = (orderMaterials.data ?? []).map((om) => ({
    value: om.orderMaterialId,
    label: `${om.materialCode} ${om.materialName}${om.materialColor ? ` - ${om.materialColor}` : ''} (${om.unit})`,
  }));
  const purchaseOrderOptions = (purchaseOrders.data?.items ?? []).map((po) => ({
    value: po.purchaseOrderId,
    label: `${po.poCode} · ${po.orderCode ?? LABELS.stockPurchaseTag} · ${po.supplierName ?? '—'}`,
  }));
  const purchaseLineOptions = (purchaseOrder.data?.lines ?? []).map((l) => {
    const outstanding = l.orderedQty - l.receivedQty - l.notReplenishedQty;
    return {
      value: l.purchaseOrderLineId,
      disabled: outstanding <= 0,
      label: `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''} · ${LABELS.outstandingQty} ${formatQty(outstanding)} ${l.unit}`,
    };
  });
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={940}
      title={receipt ? `${MESSAGES.common.edit} ${receipt.receiptCode}` : LABELS.receiptsTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Space wrap align="start">
          <Form.Item name="source" label={LABELS.receiptSource} rules={required}>
            <Radio.Group
              options={SOURCE_OPTIONS}
              optionType="button"
              disabled={receipt !== null}
              onChange={resetLines}
            />
          </Form.Item>
          {isSupplier ? (
            <Form.Item name="purchaseOrderId" label={LABELS.purchaseOrder} rules={required}>
              <Select
                showSearch
                optionFilterProp="label"
                placeholder={LABELS.selectPurchaseOrder}
                options={purchaseOrderOptions}
                loading={purchaseOrders.isFetching}
                style={{ width: 420 }}
                onChange={resetLines}
              />
            </Form.Item>
          ) : (
            <Form.Item name="orderId" label={LABELS.order} rules={required}>
              <OrderSelect onChange={resetLines} />
            </Form.Item>
          )}
          <Form.Item name="receiptDate" label={LABELS.receiptDate} rules={required}>
            <DatePicker format={DATE_FORMAT} />
          </Form.Item>
        </Space>
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="start" wrap>
                  {isSupplier ? (
                    <Form.Item name={[field.name, 'purchaseOrderLineId']} rules={required}>
                      <Select
                        placeholder={LABELS.purchaseLine}
                        options={purchaseLineOptions}
                        style={{ width: 420 }}
                      />
                    </Form.Item>
                  ) : (
                    <Form.Item name={[field.name, 'orderMaterialId']} rules={required}>
                      <Select
                        placeholder={LABELS.material}
                        options={customerOptions}
                        style={{ width: 360 }}
                      />
                    </Form.Item>
                  )}
                  <Form.Item name={[field.name, 'acceptedQty']} rules={required}>
                    <InputNumber addonBefore={LABELS.acceptedQty} min={0} style={{ width: 160 }} />
                  </Form.Item>
                  <Button
                    icon={<DeleteOutlined />}
                    onClick={() => remove(field.name)}
                    disabled={fields.length === 1}
                  />
                </Space>
              ))}
              <Button
                type="dashed"
                icon={<PlusOutlined />}
                onClick={() => add(EMPTY_LINE)}
                disabled={isSupplier ? !purchaseOrderId : !orderId}
              >
                {LABELS.addLine}
              </Button>
            </>
          )}
        </Form.List>
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
