import {
  Button,
  Form,
  Input,
  InputNumber,
  Modal,
  Radio,
  Select,
  Space,
  Upload,
  message,
} from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { AttachmentGallery } from '@/components/common/attachments';
import { ATTACHMENT_ACCEPT, MAX_ATTACHMENT_BYTES } from '@/constants/attachments';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { LABELS, MATERIAL_SOURCE, MAX_RECEIPT_PHOTOS, SOURCE_LABEL } from '../constants';
import type { MaterialReceipt, ReceiptPreset } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppDatePicker } from '@/components/common/DateInputs';
import { useReceiptForm } from '../hooks/useReceiptForm';

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
    isSupplier,
    purchaseOrders,
    resetLines,
    handleFinish,
    customerOptions,
    purchaseOrderOptions,
    purchaseLineOptions,
    addableOptions,
    handleClose,
    photos,
    setPhotos,
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
      onCancel={handleClose}
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
                <Space key={field.key} align="start" wrap style={{ display: 'flex' }}>
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
              <Select
                placeholder={LABELS.addReceiptLine}
                value={null}
                disabled={addableOptions.length === 0}
                style={{ width: 420 }}
                options={addableOptions.map(({ value, label }) => ({ value, label }))}
                onChange={(value: number) => {
                  const option = addableOptions.find((o) => o.value === value);
                  if (!option) return;
                  add({
                    [isSupplier ? 'purchaseOrderLineId' : 'orderMaterialId']: value,
                    acceptedQty: option.outstanding,
                  });
                }}
              />
            </>
          )}
        </Form.List>
        <Form.Item
          label={LABELS.documentPhotos}
          extra={LABELS.documentPhotosHint}
          style={{ marginTop: 16 }}
        >
          {receipt ? (
            <AttachmentGallery
              ownerType={ATTACHMENT_OWNER.MaterialReceipt}
              ownerId={receipt.materialReceiptId}
              canEdit
              maxFiles={MAX_RECEIPT_PHOTOS}
              purpose={ATTACHMENT_PURPOSE.DocumentPhoto}
            />
          ) : (
            <Upload
              listType="picture-card"
              accept={ATTACHMENT_ACCEPT}
              fileList={photos}
              beforeUpload={(f) => {
                if (f.size > MAX_ATTACHMENT_BYTES) {
                  void message.error(MESSAGES.attachments.tooLarge);
                  return Upload.LIST_IGNORE;
                }
                return false; // giữ file ở client, tải lên sau khi lưu phiếu
              }}
              onChange={({ fileList }) => setPhotos(fileList.slice(0, MAX_RECEIPT_PHOTOS))}
            >
              {photos.length < MAX_RECEIPT_PHOTOS && <PlusOutlined />}
            </Upload>
          )}
        </Form.Item>
        <Form.Item name="note" label={LABELS.note}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
