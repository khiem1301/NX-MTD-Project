import { useEffect } from 'react';
import { DatePicker, Descriptions, Form, InputNumber, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useSavePieceRate } from '../hooks/useProduction';
import type { Operation } from '../types';
import { editableRate } from '../utils';

type Props = { operation: Operation | null; onClose: () => void };

type FormValues = { rate: number; effectiveDate: Dayjs };

const text = PRODUCTION_TEXT.pieceRates;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

/**
 * UC25 — Kế toán đặt đơn giá cho một công đoạn: sửa bản Nháp / bị từ chối nếu có, không thì lập bản mới theo ngày hiệu lực.
 * Đơn giá chỉ có hiệu lực khi Admin duyệt (UC26).
 */
export function PieceRateFormModal({ operation, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const editing = operation ? editableRate(operation) : null;
  const save = useSavePieceRate(editing?.pieceRateId ?? null);

  useEffect(() => {
    if (!operation) return;
    form.resetFields();
    if (editing) form.setFieldsValue({ rate: editing.rate, effectiveDate: dayjs(editing.effectiveDate) });
  }, [operation, editing, form]);

  const handleFinish = (values: FormValues) => {
    if (!operation) return;
    save.mutate(
      { operationId: operation.operationId, rate: values.rate, effectiveDate: toApiDate(values.effectiveDate) },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={operation !== null}
      width={480}
      title={editing ? text.edit : text.add}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {operation && (
        <Descriptions size="small" column={1} style={{ marginBottom: 16 }}>
          <Descriptions.Item label={text.form.operation}>{`${operation.operationCode} - ${operation.operationName}`}</Descriptions.Item>
          <Descriptions.Item label={text.form.currentRate}>
            {operation.currentRate === null ? '—' : formatCurrency(operation.currentRate)}
          </Descriptions.Item>
        </Descriptions>
      )}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item name="rate" label={text.form.rate} rules={[...required, { type: 'number', min: 0, message: text.form.rateMin }]}>
          <InputNumber min={0} precision={2} style={{ width: 220 }} />
        </Form.Item>
        <Form.Item name="effectiveDate" label={text.form.effectiveDate} rules={required}>
          <DatePicker format="DD/MM/YYYY" style={{ width: 220 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
