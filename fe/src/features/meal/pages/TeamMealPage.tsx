import { Alert, Spin } from 'antd';
import { MEAL_TEXT } from '../constants';
import { TeamMealConfirmForm } from '../components/TeamMealConfirmForm';
import { useTeamMealSuggestion } from '../hooks/useMeal';
import { toApiDate, today } from '../utils';
import { PageCard } from '@/components/common/PageCard';

/** UC30 — tổ trưởng/tổ phó xem gợi ý và xác nhận suất ăn tổ mình hôm nay (GBR-01, phạm vi do BE ép). */
export function TeamMealPage() {
  const date = toApiDate(today());
  const { data, isLoading, error } = useTeamMealSuggestion(date);

  return (
    <PageCard title={MEAL_TEXT.teamPageTitle} description={MEAL_TEXT.teamPageSubtitle}>
      {error ? (
        <Alert type="error" showIcon message={(error as { message: string }).message} />
      ) : null}
      {isLoading ? <Spin /> : null}
      {data ? <TeamMealConfirmForm date={date} suggestion={data} /> : null}
    </PageCard>
  );
}
