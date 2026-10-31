import { Alert, Spin, Tabs } from 'antd';
import { MEAL_TEXT } from '../constants';
import { GuestMealForm } from '../components/GuestMealForm';
import { MealPriceForm } from '../components/MealPriceForm';
import { MealSummaryTable } from '../components/MealSummaryTable';
import { useMealSummary } from '../hooks/useMeal';
import { toApiDate, today } from '../utils';
import { PageCard } from '@/components/common/PageCard';

/** UC30 (FR 5.2–5.4) / MI-04 — Kế toán: suất khách, tổng hợp suất ăn ngày, báo bếp, đơn giá. */
export function MealSummaryPage() {
  const date = toApiDate(today());
  const { data, isLoading, error } = useMealSummary(date);

  return (
    <PageCard title={MEAL_TEXT.summaryPageTitle} description={MEAL_TEXT.summaryPageSubtitle}>
      {error ? (
        <Alert type="error" showIcon message={(error as { message: string }).message} />
      ) : null}
      {isLoading || !data ? (
        <Spin />
      ) : (
        <Tabs
          defaultActiveKey="summary"
          items={[
            {
              key: 'summary',
              label: MEAL_TEXT.tabs.summary,
              children: <MealSummaryTable date={date} summary={data} />,
            },
            {
              key: 'guest',
              label: MEAL_TEXT.tabs.guest,
              children: <GuestMealForm date={date} summary={data} />,
            },
            {
              key: 'price',
              label: MEAL_TEXT.tabs.price,
              children: <MealPriceForm date={date} summary={data} />,
            },
          ]}
        />
      )}
    </PageCard>
  );
}
