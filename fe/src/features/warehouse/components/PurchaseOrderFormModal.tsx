import { useEffect } from 'react';
import { Alert, Button, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useOrderMaterials } from '../hooks/useOrderMaterials';
import { useCreatePurchaseOrder, useSupplierOptions } from '../hooks/usePurchaseOrders';
import type { CreatePurchaseOrderPayload } from '../types';
import { isNewMaterial, newMaterialName } from '../newMaterial';
import { MaterialFreeSelect } from './MaterialFreeSelect';
import { MODAL_WIDTH } from '@/components/common/modalSize';

type PurchaseOrderFormModalProps = {
  open: boolean;
  onClose: () => void;
};

type LineValues = {
  material?: number | string;
  unit?: string;
  newMaterialColor?: string;
  orderedQty: number;
  unitPrice: number;
};
type FormValues = { orderId?: number; supplierId?: number; note?: string; lines: LineValues[] };

const EMPTY_LINE: LineValues = { orderedQty: 1, unitPrice: 0 };

const toPayload = (values: FormValues): CreatePurchaseOrderPayload => ({
  orderId: values.orderId,
  supplierId: values.supplierId as number,
  note: values.note,
  lines: values.lines.map(({ material, unit, newMaterialColor, ...rest }) =>
    isNewMaterial(material)
      ? {
          ...rest,
          newMaterialName: newMaterialName(material),
          newMaterialUnit: unit,
          newMaterialColor,
        }
      : { ...rest, materialId: material },
  ),
});

/**
 * Quản đốc tạo đơn mua NVL. Chọn đơn hàng: các dòng lấy từ NVL của đơn (phần "phải mua"), chỉ nhập đơn giá.
 * Để trống đơn hàng: mua dự trữ dùng chung (kim, chỉ...); NVL chưa có trong danh mục thì gõ tên + đơn vị để thêm mới.
 */
export function PurchaseOrderFormModal({ open, onClose }: PurchaseOrderFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreatePurchaseOrder();
  const orderId = Form.useWatch('orderId', form);
  const orderMaterials = useOrderMaterials(orderId);
  const linked = orderId !== undefined;
  const suppliers = useSupplierOptions(open);
  const lines = Form.useWatch('lines', form);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({ lines: [EMPTY_LINE] });
  }, [open, form]);

  // Chọn đơn hàng → điền sẵn các dòng NVL còn phải mua của đơn; bỏ chọn → về một dòng trống.
  useEffect(() => {
    if (!linked) {
      form.setFieldsValue({ lines: [EMPTY_LINE] });
      return;
    }
    if (!orderMaterials.data) return;
    form.setFieldsValue({
      lines: orderMaterials.data
        .filter((om) => (om.purchaseQty ?? 0) > 0)
        .map<LineValues>((om) => ({
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
        onFinish={(values) => create.mutate(toPayload(values), { onSuccess: onClose })}
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
                <Space key={field.key} align="start" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'material']} rules={required}>
                    <MaterialFreeSelect disabled={linked} />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) =>
                      isNewMaterial(getFieldValue(['lines', field.name, 'material'])) && (
                        <Space align="start">
                          <Form.Item name={[field.name, 'unit']} rules={required}>
                            <Input
                              placeholder={LABELS.unit}
                              maxLength={20}
                              style={{ width: 110 }}
                            />
                          </Form.Item>
                          <Form.Item name={[field.name, 'newMaterialColor']}>
                            <Input
                              placeholder={LABELS.color}
                              maxLength={50}
                              style={{ width: 130 }}
                            />
                          </Form.Item>
                        </Space>
                      )
                    }
                  </Form.Item>
                  <Form.Item name={[field.name, 'orderedQty']} rules={required}>
                    <InputNumber
                      addonBefore={LABELS.orderedQty}
                      min={0.001}
                      style={{ width: 170 }}
                    />
                  </Form.Item>
                  <Form.Item name={[field.name, 'unitPrice']} rules={required}>
                    <InputNumber addonBefore={LABELS.unitPrice} min={0} style={{ width: 190 }} />
                  </Form.Item>
                  <Button
                    icon={<DeleteOutlined />}
                    onClick={() => remove(field.name)}
                    disabled={fields.length === 1}
                  />
                </Space>
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
                  <Button type="dashed" icon={<PlusOutlined />} onClick={() => add(EMPTY_LINE)}>
                    {LABELS.addLine}
                  </Button>
                )}
              </div>
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
