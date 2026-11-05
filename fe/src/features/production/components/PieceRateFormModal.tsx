import { useEffect } from 'react';
import { Alert, Descriptions, Form, InputNumber, Modal } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, toApiDate } from '@/utils/format';
import { PIECE_RATE_LIMITS, PRODUCTION_TEXT } from '../constants';
import { useSavePieceRate } from '../hooks/usePieceRates';
import type { Operation } from '../types';
import { editableRate } from '../utils/outputRules';

import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';

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
  const date = Form.useWatch('effectiveDate', form);
  const isPastDate = date !== undefined && date !== null && date.isBefore(dayjs(), 'day');

  useEffect(() => {
    if (!operation) return;
    form.resetFields();
    if (editing)
      form.setFieldsValue({ rate: editing.rate, effectiveDate: dayjs(editing.effectiveDate) });
    else form.setFieldsValue({ effectiveDate: dayjs() });
  }, [operation, editing, form]);

  const handleFinish = (values: FormValues) => {
    if (!operation) return;
    save.mutate(
      {
        operationId: operation.operationId,
        rate: values.rate,
        effectiveDate: toApiDate(values.effectiveDate),
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={operation !== null}
      width={MODAL_WIDTH.sm}
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
          <Descriptions.Item
            label={text.form.operation}
          >{`${operation.operationCode} - ${operation.operationName}`}</Descriptions.Item>
          <Descriptions.Item label={text.form.currentRate}>
            {operation.currentRate === null ? '—' : formatCurrency(operation.currentRate)}
          </Descriptions.Item>
        </Descriptions>
      )}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item
          name="rate"
          label={text.form.rate}
          rules={[
            ...required,
            { type: 'number', min: PIECE_RATE_LIMITS.min, message: text.form.rateMin },
            { type: 'number', max: PIECE_RATE_LIMITS.max, message: text.form.rateMax },
          ]}
        >
          <InputNumber precision={2} style={{ width: 220 }} />
        </Form.Item>
        <Form.Item name="effectiveDate" label={text.form.effectiveDate} rules={required}>
          <AppDatePicker style={{ width: 220 }} />
        </Form.Item>
        {isPastDate && <Alert type="warning" showIcon message={text.form.pastDateHint} />}
      </Form>
    </Modal>
  );
}
