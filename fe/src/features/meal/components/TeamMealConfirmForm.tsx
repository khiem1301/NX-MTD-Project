import { useEffect } from 'react';
import { Alert, Button, Descriptions, Form, InputNumber, Space, Tag } from 'antd';
import { formatDateTime } from '@/utils/format';
import { MEAL_TEXT } from '../constants';
import { useConfirmTeamMeal } from '../hooks/useMeal';
import type { TeamMealSuggestion } from '../types';

type FormValues = { mealCount: number };

type Props = {
  date: string;
  suggestion: TeamMealSuggestion;
};

const sourceTone: Record<'Confirmed' | 'Attendance' | 'none', string> = {
  Confirmed: 'green',
  Attendance: 'blue',
  none: 'default',
};

/** UC30 (FR 5.1) — tổ trưởng/tổ phó xác nhận số suất ăn của tổ, gợi ý sẵn từ chấm công hôm nay. */
export function TeamMealConfirmForm({ date, suggestion }: Props) {
  const [form] = Form.useForm<FormValues>();
  const confirm = useConfirmTeamMeal(date);

  useEffect(() => {
    form.setFieldsValue({ mealCount: suggestion.mealCount ?? suggestion.suggestedCount });
  }, [form, suggestion.mealCount, suggestion.suggestedCount]);

  const sourceKey = suggestion.source ?? 'none';
  const sourceLabel =
    sourceKey === 'Confirmed'
      ? MEAL_TEXT.suggestion.sourceConfirmed
      : sourceKey === 'Attendance'
        ? MEAL_TEXT.suggestion.sourceAttendance
        : MEAL_TEXT.suggestion.sourceNone;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: 480 }}>
      <Descriptions column={1} bordered size="small">
        <Descriptions.Item label={MEAL_TEXT.suggestion.suggestedCount}>
          {suggestion.suggestedCount}
        </Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.summary.source}>
          <Tag color={sourceTone[sourceKey]}>{sourceLabel}</Tag>
        </Descriptions.Item>
        {suggestion.confirmedAt ? (
          <Descriptions.Item label={MEAL_TEXT.suggestion.confirmedAt}>
            {formatDateTime(suggestion.confirmedAt)}
          </Descriptions.Item>
        ) : null}
      </Descriptions>

      {suggestion.isLocked ? (
        <Alert type="warning" showIcon message={MEAL_TEXT.suggestion.lockedHint} />
      ) : null}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => confirm.mutate({ mealDate: date, mealCount: values.mealCount })}
      >
        <Form.Item
          name="mealCount"
          label={MEAL_TEXT.suggestion.mealCount}
          rules={[
            { required: true, message: 'Vui lòng nhập số suất ăn.' },
            { type: 'number', min: 0, message: 'Số suất ăn phải lớn hơn hoặc bằng 0.' },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} disabled={suggestion.isLocked} />
        </Form.Item>
        <Button
          type="primary"
          htmlType="submit"
          loading={confirm.isPending}
          disabled={suggestion.isLocked}
        >
          {MEAL_TEXT.suggestion.confirm}
        </Button>
      </Form>
    </Space>
  );
}
