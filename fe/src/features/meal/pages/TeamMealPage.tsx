import dayjs from 'dayjs';
import { Alert, Spin } from 'antd';
import { PageCard } from '@/components/common/PageCard';
import { toApiDate } from '@/utils/format';
import { TeamMealMemberList } from '../components/TeamMealMemberList';
import { MEAL_TEXT } from '../constants';
import { useTeamMealMembers, useTeamMealSuggestion } from '../hooks/useMeal';

/** UC30 — tổ trưởng/tổ phó rà soát checklist suất ăn tổ mình hôm nay. */
export function TeamMealPage() {
  const date = toApiDate(dayjs());
  const suggestion = useTeamMealSuggestion(date);
  const members = useTeamMealMembers(date);

  const error = suggestion.error ?? members.error;
  const isLoading = suggestion.isLoading || members.isLoading;

  return (
    <PageCard title={MEAL_TEXT.teamPageTitle} description={MEAL_TEXT.teamPageSubtitle}>
      {error ? (
        <Alert type="error" showIcon message={(error as { message: string }).message} />
      ) : null}
      {isLoading ? <Spin /> : null}
      {suggestion.data && members.data ? (
        <TeamMealMemberList date={date} members={members.data} suggestion={suggestion.data} />
      ) : null}
    </PageCard>
  );
}
