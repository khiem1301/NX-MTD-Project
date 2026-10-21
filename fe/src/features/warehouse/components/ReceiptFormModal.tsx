import { useEffect } from 'react';
import { Button, DatePicker, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { LABELS, MATERIAL_SOURCE } from '../constants';
import { useOrderMaterials } from '../hooks/useOrderMaterials';
import { useSaveReceipt } from '../hooks/useReceipts';
import type { MaterialReceipt, SaveReceiptPayload } from '../types';

type ReceiptFormModalProps = {
  open: boolean;
  receipt: MaterialReceipt | null;
  onClose: () => void;
};

type FormValues = Omit<SaveReceiptPayload, 'receiptDate' | 'source'> & { receiptDate: Dayjs };

const DATE_FORMAT = 'DD/MM/YYYY';
const API_DATE = 'YYYY-MM-DD';

/** UC37: Thủ kho lập / sửa phiếu nhập NVL khách cấp (số đạt, thiếu, lỗi từng dòng). */
export function ReceiptFormModal({ open, receipt, onClose }: ReceiptFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveReceipt();
  const orderId = Form.useWatch('orderId', form);
  const orderMaterials = useOrderMaterials(orderId);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      receipt
        ? {
            ...receipt,
            orderId: receipt.orderId ?? undefined,
            note: receipt.note ?? undefined,
            receiptDate: dayjs(receipt.receiptDate),
          }
        : { receiptDate: dayjs(), lines: [{ acceptedQty: 0, shortQty: 0, defectiveQty: 0 }] },
    );
  }, [open, receipt, form]);

  const handleFinish = (values: FormValues) => {
    const body: SaveReceiptPayload = {
      ...values,
      source: MATERIAL_SOURCE.CUSTOMER,
      receiptDate: values.receiptDate.format(API_DATE),
    };
    save.mutate({ id: receipt?.materialReceiptId, body }, { onSuccess: onClose });
  };

  const materialOptions = (orderMaterials.data ?? []).map((om) => ({
    value: om.orderMaterialId,
    label: `${om.materialCode} ${om.materialName}${om.materialColor ? ` - ${om.materialColor}` : ''} (${om.unit})`,
  }));
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={900}
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
          <Form.Item name="orderId" label={LABELS.order} rules={required}>
            <OrderSelect
              onChange={() =>
                form.setFieldValue('lines', [{ acceptedQty: 0, shortQty: 0, defectiveQty: 0 }])
              }
            />
          </Form.Item>
          <Form.Item name="receiptDate" label={LABELS.receiptDate} rules={required}>
            <DatePicker format={DATE_FORMAT} />
          </Form.Item>
        </Space>
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="start" wrap>
                  <Form.Item name={[field.name, 'orderMaterialId']} rules={required}>
                    <Select
                      placeholder={LABELS.material}
                      options={materialOptions}
                      style={{ width: 360 }}
                    />
                  </Form.Item>
                  <Form.Item name={[field.name, 'acceptedQty']} rules={required}>
                    <InputNumber addonBefore={LABELS.acceptedQty} min={0} style={{ width: 160 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'shortQty']} rules={required}>
                    <InputNumber addonBefore={LABELS.shortQty} min={0} style={{ width: 150 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'defectiveQty']} rules={required}>
                    <InputNumber addonBefore={LABELS.defectiveQty} min={0} style={{ width: 150 }} />
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
                onClick={() => add({ acceptedQty: 0, shortQty: 0, defectiveQty: 0 })}
                disabled={!orderId}
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
