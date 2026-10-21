import { useEffect } from 'react';
import { DatePicker, Form, InputNumber, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useOperations, useSavePieceRate } from '../hooks/useProduction';
import type { PieceRate } from '../types';
import { ProductSelect } from './ProductSelect';

type Props = { open: boolean; pieceRate: PieceRate | null; onClose: () => void };

type FormValues = { productId: number; operationId: number; rate: number; effectiveDate: Dayjs };

const text = PRODUCTION_TEXT.pieceRates;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

/** UC25 — Kế toán lập / sửa đơn giá công đoạn (Nháp). Sửa bản đã bị từ chối thì quay về Nháp, gửi duyệt lại. */
export function PieceRateFormModal({ open, pieceRate, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSavePieceRate(pieceRate?.pieceRateId ?? null);
  const productId = Form.useWatch('productId', form) ?? null;
  const operations = useOperations(productId);
  const editing = pieceRate !== null;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (pieceRate) {
      form.setFieldsValue({
        productId: pieceRate.productId,
        operationId: pieceRate.operationId,
        rate: pieceRate.rate,
        effectiveDate: dayjs(pieceRate.effectiveDate),
      });
    }
  }, [open, pieceRate, form]);

  const operationOptions = (operations.data ?? [])
    .filter((o) => o.status === 'Active')
    .map((o) => ({ value: o.operationId, label: `${o.operationCode} - ${o.operationName}` }));

  const handleFinish = (values: FormValues) => {
    save.mutate(
      { operationId: values.operationId, rate: values.rate, effectiveDate: toApiDate(values.effectiveDate) },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={520}
      title={editing ? text.edit : text.add}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item name="productId" label={PRODUCTION_TEXT.selectProduct} rules={required}>
          <ProductSelect width={440} disabled={editing} onChange={() => form.setFieldValue('operationId', undefined)} />
        </Form.Item>
        <Form.Item name="operationId" label={text.form.operation} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            options={operationOptions}
            loading={operations.isLoading}
            disabled={editing || productId === null}
          />
        </Form.Item>
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
