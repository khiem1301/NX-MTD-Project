import { Alert, Space, Spin, Typography } from 'antd';
import { MEAL_TEXT } from '../constants';
import { TeamMealConfirmForm } from '../components/TeamMealConfirmForm';
import { useTeamMealSuggestion } from '../hooks/useMeal';
import { toApiDate, today } from '../utils';

/** UC30 — tổ trưởng/tổ phó xem gợi ý và xác nhận suất ăn tổ mình hôm nay (GBR-01, phạm vi do BE ép). */
export function TeamMealPage() {
  const date = toApiDate(today());
  const { data, isLoading, error } = useTeamMealSuggestion(date);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {MEAL_TEXT.teamPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{MEAL_TEXT.teamPageSubtitle}</Typography.Text>
      </div>

      {error ? <Alert type="error" showIcon message={(error as { message: string }).message} /> : null}
      {isLoading ? <Spin /> : null}
      {data ? <TeamMealConfirmForm date={date} suggestion={data} /> : null}
    </Space>
  );
}
