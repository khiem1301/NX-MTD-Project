import { useEffect } from 'react';
import { Button, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Space, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import type { Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE } from '@/constants/requestTypes';
import { useCreateRequest } from '@/features/requests/hooks/useRequests';
import { formatDate, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { PRODUCTION_KEY } from '../hooks/useProduction';
import type { Defect } from '../types';
import { modalWidth } from '../utils';

type Props = { defect: Defect | null; onClose: () => void };

type FormValues = { panels: { panelName: string; quantity: number }[]; targetDate?: Dayjs; content?: string };

const text = PRODUCTION_TEXT.replacement;

/**
 * KI-09 (UC56, UC07 "cắt bù") — tổ trưởng / tổ phó tạo đơn cắt bù từ một phiếu hàng hỏng Scrap, gửi Quản đốc duyệt.
 * Mỗi chi tiết là một dòng đơn (tên chi tiết + số lượng); BE kiểm phiếu, tổ, dòng đơn và số lượng.
 */
export function ReplacementCuttingModal({ defect, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreateRequest();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (defect) {
      form.resetFields();
      form.setFieldsValue({ panels: [{ panelName: '', quantity: Math.min(1, defect.quantity) }] });
    }
  }, [defect, form]);

  const handleFinish = (values: FormValues) => {
    if (!defect) return;
    create.mutate(
      {
        requestType: REQUEST_TYPE.ReplacementCutting,
        title: `Cắt bù ${defect.defectCode}`,
        content: values.content?.trim() || null,
        targetDate: values.targetDate ? toApiDate(values.targetDate) : null,
        orderId: defect.orderId,
        defectRecordId: defect.defectRecordId,
        lines: values.panels.map((p) => ({
          orderLineId: defect.orderLineId,
          panelName: p.panelName.trim(),
          quantity: p.quantity,
        })),
      },
      {
        onSuccess: () => {
          void message.success(text.done);
          void queryClient.invalidateQueries({ queryKey: PRODUCTION_KEY });
          onClose();
        },
      },
    );
  };

  return (
    <Modal
      open={defect !== null}
      width={modalWidth(560)}
      title={text.title}
      okText={text.submit}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {defect && (
        <Descriptions size="small" column={1} style={{ marginBottom: 12 }}>
          <Descriptions.Item label={text.defect}>
            {`${defect.defectCode} · ${defect.orderCode} · ${defect.productCode} · ${defect.quantity} sản phẩm hủy (${formatDate(defect.workDate)})`}
          </Descriptions.Item>
        </Descriptions>
      )}
      <Typography.Paragraph type="secondary">{text.hint}</Typography.Paragraph>

      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={create.isPending}>
        <Form.Item label={text.panels} required style={{ marginBottom: 8 }}>
          <Form.List name="panels">
            {(fields, { add, remove }) => (
              <Space direction="vertical" style={{ width: '100%' }}>
                {fields.map((field) => (
                  <Space key={field.key} align="baseline" style={{ display: 'flex' }}>
                    <Form.Item
                      name={[field.name, 'panelName']}
                      rules={[{ required: true, whitespace: true, message: text.panelRequired }]}
                      style={{ marginBottom: 0 }}
                    >
                      <Input placeholder={text.panelName} style={{ width: 280 }} maxLength={100} />
                    </Form.Item>
                    <Form.Item
                      name={[field.name, 'quantity']}
                      rules={[{ required: true, type: 'number', min: 1, max: defect?.quantity, message: text.qtyRange }]}
                      style={{ marginBottom: 0 }}
                    >
                      <InputNumber min={1} max={defect?.quantity} precision={0} placeholder={text.quantity} />
                    </Form.Item>
                    {fields.length > 1 && <MinusCircleOutlined onClick={() => remove(field.name)} />}
                  </Space>
                ))}
                <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({ panelName: '', quantity: 1 })}>
                  {text.addPanel}
                </Button>
              </Space>
            )}
          </Form.List>
        </Form.Item>
        <Form.Item name="targetDate" label={text.targetDate}>
          <DatePicker format="DD/MM/YYYY" />
        </Form.Item>
        <Form.Item name="content" label={text.content}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
