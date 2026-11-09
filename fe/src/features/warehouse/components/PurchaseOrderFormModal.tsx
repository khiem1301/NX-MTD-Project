import { useEffect } from 'react';
import dayjs from 'dayjs';
import { Alert, Button, Form, Input, Modal, Select, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useOrderMaterials } from '../hooks/useOrderMaterials';
import { useCreatePurchaseOrder, useSupplierOptions } from '../hooks/usePurchaseOrders';
import {
  EMPTY_PURCHASE_LINE,
  toPurchaseOrderPayload,
  type PurchaseOrderFormValues,
  type PurchaseOrderLineValues,
} from '../utils/purchaseOrderForm';
import { PurchaseOrderLineRow } from './PurchaseOrderLineRow';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { formatCurrency } from '@/utils/format';
import { purchaseOrderTotal } from '../utils/purchaseOrderTotal';
import { PurchaseOrderTimingFields } from './PurchaseOrderTimingFields';

type PurchaseOrderFormModalProps = {
  open: boolean;
  onClose: () => void;
};

/**
 * Quản đốc tạo đơn mua NVL, tạo xong là đặt hàng luôn (nhà cung cấp, số lượng, đơn giá, ngày đặt, số lần giao). Chọn đơn hàng: các dòng lấy từ NVL của đơn (phần "phải mua"), nhập đơn giá.
 * Để trống đơn hàng: mua dự trữ dùng chung (kim, chỉ...); NVL chưa có trong danh mục thì gõ tên + đơn vị để thêm mới.
 */
export function PurchaseOrderFormModal({ open, onClose }: PurchaseOrderFormModalProps) {
  const [form] = Form.useForm<PurchaseOrderFormValues>();
  const create = useCreatePurchaseOrder();
  const orderId = Form.useWatch('orderId', form);
  const orderMaterials = useOrderMaterials(orderId);
  const linked = orderId !== undefined;
  const suppliers = useSupplierOptions(open);
  const lines = Form.useWatch('lines', form);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      orderDate: dayjs(),
      expectedDeliveryCount: 1,
      lines: [EMPTY_PURCHASE_LINE],
    });
  }, [open, form]);

  // Chọn đơn hàng → điền sẵn các dòng NVL còn phải mua của đơn; bỏ chọn → về một dòng trống.
  useEffect(() => {
    if (!linked) {
      form.setFieldsValue({ lines: [EMPTY_PURCHASE_LINE] });
      return;
    }
    if (!orderMaterials.data) return;
    form.setFieldsValue({
      lines: orderMaterials.data
        .filter((om) => (om.purchaseQty ?? 0) > 0)
        .map<PurchaseOrderLineValues>((om) => ({
          material: om.materialId,
          orderedQty: om.purchaseQty ?? 0,
          unitPrice: 0,
        })),
    });
  }, [linked, orderMaterials.data, form]);

  // Đơn hàng không bắt buộc mua hết NVL: dòng nào chưa mua thì xóa, cần lại thì chọn thêm từ NVL của đơn.
  const buyable = (orderMaterials.data ?? []).filter((om) => (om.purchaseQty ?? 0) > 0);
  const addable = buyable.filter((om) => !(lines ?? []).some((l) => l.material === om.materialId));
  const nothingToBuy =
    linked &&
    orderMaterials.data !== undefined &&
    !orderMaterials.data.some((om) => (om.purchaseQty ?? 0) > 0);
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={LABELS.stockPurchase}
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
        onFinish={(values) => create.mutate(toPurchaseOrderPayload(values), { onSuccess: onClose })}
        disabled={create.isPending}
      >
        <Form.Item name="supplierId" label={LABELS.supplier} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={suppliers.isLoading}
            style={{ width: 380 }}
            options={(suppliers.data ?? []).map((s) => ({
              value: s.supplierId,
              label: s.supplierName,
            }))}
          />
        </Form.Item>
        {suppliers.isSuccess && suppliers.data.length === 0 && (
          <Alert
            type="warning"
            showIcon
            message={LABELS.noActiveSupplier}
            style={{ marginBottom: 12 }}
          />
        )}
        <Form.Item
          name="orderId"
          label={LABELS.purchaseOrderOrder}
          extra={LABELS.purchaseOrderOrderHint}
        >
          <OrderSelect allowClear placeholder={LABELS.stockPurchaseTag} width={380} />
        </Form.Item>
        <PurchaseOrderTimingFields />
        {nothingToBuy && (
          <Alert
            type="warning"
            showIcon
            message={LABELS.nothingToBuy}
            style={{ marginBottom: 12 }}
          />
        )}
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <PurchaseOrderLineRow
                  key={field.key}
                  name={field.name}
                  linked={linked}
                  canRemove={fields.length > 1}
                  onRemove={() => remove(field.name)}
                />
              ))}
              <div style={{ marginTop: 4 }}>
                {linked ? (
                  <Select
                    placeholder={LABELS.addOrderMaterial}
                    value={null}
                    disabled={addable.length === 0}
                    style={{ width: 380 }}
                    options={addable.map((om) => ({
                      value: om.materialId,
                      label: `${om.materialCode} - ${om.materialName}`,
                    }))}
                    onChange={(materialId: number) => {
                      const om = addable.find((x) => x.materialId === materialId);
                      if (om)
                        add({
                          material: om.materialId,
                          orderedQty: om.purchaseQty ?? 1,
                          unitPrice: 0,
                        });
                    }}
                  />
                ) : (
                  <Button
                    type="dashed"
                    icon={<PlusOutlined />}
                    onClick={() => add(EMPTY_PURCHASE_LINE)}
                  >
                    {LABELS.addLine}
                  </Button>
                )}
              </div>
            </>
          )}
        </Form.List>
        <Typography.Paragraph strong style={{ marginTop: 16 }} data-testid="purchase-order-total">
          {`${LABELS.purchaseOrderTotal}: ${formatCurrency(purchaseOrderTotal(lines))}`}
        </Typography.Paragraph>
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
