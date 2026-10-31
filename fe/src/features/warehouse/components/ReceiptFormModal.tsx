import { Button, Form, Input, InputNumber, Modal, Radio, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { LABELS, MATERIAL_SOURCE, SOURCE_LABEL } from '../constants';
import type { MaterialReceipt, ReceiptPreset } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';
import { useReceiptForm } from '../hooks/useReceiptForm';
import { EMPTY_LINE } from '../hooks/useReceiptForm';

export type ReceiptFormModalProps = {
  open: boolean;
  receipt: MaterialReceipt | null;
  /** Điền sẵn khi lập từ màn NVL chờ nhận (bỏ qua khi sửa phiếu). */
  preset?: ReceiptPreset | null;
  onClose: () => void;
};

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
  const {
    save,
    orderId,
    purchaseOrderId,
    isSupplier,
    purchaseOrders,
    resetLines,
    handleFinish,
    customerOptions,
    purchaseOrderOptions,
    purchaseLineOptions,
    required,
    form,
  } = useReceiptForm({ open, receipt, preset, onClose });

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
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
            <AppDatePicker />
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
