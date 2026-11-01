import { useEffect } from 'react';
import { Alert, Button, Form, Input, InputNumber, Space } from 'antd';
import { MEAL_TEXT } from '../constants';
import { useSetGuestMeal } from '../hooks/useMeal';
import type { DailyMealSummary } from '../types';

type FormValues = { guestCount: number; guestNote?: string };

type Props = {
  date: string;
  summary: DailyMealSummary;
};

/** UC30 (FR 5.2) — Kế toán nhập suất khách; chỉ sửa được khi ngày chưa chốt (MSG22). */
export function GuestMealForm({ date, summary }: Props) {
  const [form] = Form.useForm<FormValues>();
  const setGuestMeal = useSetGuestMeal(date);
  const locked = summary.status !== 'Open';

  useEffect(() => {
    form.setFieldsValue({
      guestCount: summary.guestCount,
      guestNote: summary.guestNote ?? undefined,
    });
  }, [form, summary.guestCount, summary.guestNote]);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: 480 }}>
      {locked ? <Alert type="warning" showIcon message={MEAL_TEXT.guestForm.lockedHint} /> : null}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) =>
          setGuestMeal.mutate({
            mealDate: date,
            guestCount: values.guestCount,
            guestNote: values.guestNote ?? null,
          })
        }
      >
        <Form.Item
          name="guestCount"
          label={MEAL_TEXT.guestForm.guestCount}
          rules={[
            { required: true, message: 'Vui lòng nhập số suất khách.' },
            { type: 'number', min: 0, message: 'Số suất khách phải lớn hơn hoặc bằng 0.' },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} disabled={locked} />
        </Form.Item>

        <Form.Item name="guestNote" label={MEAL_TEXT.guestForm.guestNote}>
          <Input.TextArea rows={2} maxLength={300} disabled={locked} />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={setGuestMeal.isPending} disabled={locked}>
          {MEAL_TEXT.guestForm.save}
        </Button>
      </Form>
    </Space>
  );
}
