import dayjs from 'dayjs';
import { Alert, Checkbox, Spin } from 'antd';
import { PageCard } from '@/components/common/PageCard';
import { toApiDate } from '@/utils/format';
import { MEAL_TEXT } from '../constants';
import { useMyMeal, useSetMyMeal } from '../hooks/useMeal';

/** Suất ăn của tôi — bỏ tick nếu hôm nay không ăn (chỉ khi đã quẹt vân tay và chưa chốt). */
export function MyMealPage() {
  const date = toApiDate(dayjs());
  const { data, isLoading, error } = useMyMeal(date);
  const setMeal = useSetMyMeal(date);

  const punchedButNoEditableMeal =
    !!data?.hasAttendance && data.isLocked && !data.willEat;

  return (
    <PageCard title={MEAL_TEXT.myPageTitle} description={MEAL_TEXT.myPageSubtitle}>
      {error ? (
        <Alert type="error" showIcon message={(error as { message: string }).message} />
      ) : null}
      {isLoading ? <Spin /> : null}
      {data ? (
        <div style={{ maxWidth: 480 }}>
          {data.isLocked ? (
            <Alert
              type="warning"
              showIcon
              message={MEAL_TEXT.suggestion.lockedHint}
              style={{ marginBottom: 16 }}
            />
          ) : null}
          {!data.hasAttendance ? (
            <Alert type="info" showIcon message={MEAL_TEXT.my.noAttendance} />
          ) : punchedButNoEditableMeal ? (
            <Alert type="info" showIcon message={MEAL_TEXT.my.punchedAfterLock} />
          ) : (
            <Checkbox
              checked={data.willEat}
              disabled={data.isLocked || setMeal.isPending}
              onChange={(e) => setMeal.mutate({ mealDate: date, willEat: e.target.checked })}
            >
              {MEAL_TEXT.my.willEatLabel}
            </Checkbox>
          )}
        </div>
      ) : null}
    </PageCard>
  );
}
