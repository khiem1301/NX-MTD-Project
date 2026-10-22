import { useEffect } from 'react';
import { Alert, Button, Descriptions, Form, InputNumber, Space } from 'antd';
import { MEAL_TEXT } from '../constants';
import { useSetMealPrice } from '../hooks/useMeal';
import type { DailyMealSummary } from '../types';
import { formatCurrency } from '../utils';

type FormValues = { mealPrice: number };

type Props = {
  date: string;
  summary: DailyMealSummary;
};

/** UC30 (FR 5.4) — Kế toán nhập đơn giá suất ăn cho ngày đã chốt; thành tiền do DB tự tính lại. */
export function MealPriceForm({ date, summary }: Props) {
  const [form] = Form.useForm<FormValues>();
  const setMealPrice = useSetMealPrice(date);
  const closed = summary.status !== 'Open';

  useEffect(() => {
    form.setFieldsValue({ mealPrice: summary.mealPrice ?? summary.suggestedMealPrice ?? 0 });
  }, [form, summary.mealPrice, summary.suggestedMealPrice]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: 480 }}>
      {!closed ? <Alert type="warning" showIcon message={MEAL_TEXT.priceForm.disabledHint} /> : null}

      <Descriptions column={1} bordered size="small">
        <Descriptions.Item label={MEAL_TEXT.priceForm.totalMeals}>{summary.totalMeals}</Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.priceForm.totalAmount}>
          {formatCurrency(summary.totalAmount)}
        </Descriptions.Item>
      </Descriptions>

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => setMealPrice.mutate({ mealDate: date, mealPrice: values.mealPrice })}
      >
        <Form.Item
          name="mealPrice"
          label={MEAL_TEXT.priceForm.mealPrice}
          rules={[
            { required: true, message: 'Vui lòng nhập đơn giá.' },
            { type: 'number', min: 0, message: 'Đơn giá phải lớn hơn hoặc bằng 0.' },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} disabled={!closed} />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={setMealPrice.isPending} disabled={!closed}>
          {MEAL_TEXT.priceForm.save}
        </Button>
      </Form>
    </Space>
  );
}
