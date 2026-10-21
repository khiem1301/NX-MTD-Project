import { Alert, Button, Descriptions, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { formatDateTime } from '@/utils/format';
import { MEAL_TEXT } from '../constants';
import { useMarkKitchenNotified } from '../hooks/useMeal';
import type { DailyMealSummary, TeamMealRow } from '../types';

type Props = {
  date: string;
  summary: DailyMealSummary;
};

const sourceTag = (source: TeamMealRow['source']) => {
  if (source === 'Confirmed') return <Tag color="green">{MEAL_TEXT.suggestion.sourceConfirmed}</Tag>;
  if (source === 'Attendance') return <Tag color="blue">{MEAL_TEXT.suggestion.sourceAttendance}</Tag>;
  return <Tag>{MEAL_TEXT.suggestion.sourceNone}</Tag>;
};

const columns: ColumnsType<TeamMealRow> = [
  { title: MEAL_TEXT.summary.team, dataIndex: 'teamName' },
  { title: MEAL_TEXT.summary.mealCount, dataIndex: 'mealCount', align: 'right' },
  {
    title: MEAL_TEXT.summary.attendanceCount,
    dataIndex: 'attendanceCount',
    align: 'right',
    responsive: ['md'],
  },
  { title: MEAL_TEXT.summary.source, key: 'source', render: (_, row) => sourceTag(row.source) },
];

/** UC30 / MI-04 — màn tổng hợp suất ăn ngày cho Kế toán: theo tổ, văn phòng, khách, tổng, báo bếp. */
export function MealSummaryTable({ summary }: Props) {
  const markNotified = useMarkKitchenNotified(summary.mealDate);
  const canNotifyKitchen = summary.status !== 'Open' && summary.status !== 'KitchenNotified';

  return (
    <div>
      <Descriptions column={{ xs: 1, sm: 2, md: 4 }} bordered size="small" style={{ marginBottom: 16 }}>
        <Descriptions.Item label={MEAL_TEXT.summary.status}>
          <Tag>{MEAL_TEXT.statusLabel[summary.status]}</Tag>
        </Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.summary.officeCount}>{summary.officeCount}</Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.summary.guestCount}>{summary.guestCount}</Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.summary.teamTotal}>{summary.teamTotal}</Descriptions.Item>
        <Descriptions.Item label={MEAL_TEXT.summary.totalMeals}>{summary.totalMeals}</Descriptions.Item>
        {summary.kitchenNotifiedAt ? (
          <Descriptions.Item label={MEAL_TEXT.summary.kitchenNotifiedAt}>
            {formatDateTime(summary.kitchenNotifiedAt)}
          </Descriptions.Item>
        ) : null}
      </Descriptions>

      {summary.attendanceIncomplete ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={MEAL_TEXT.summary.attendanceIncomplete}
        />
      ) : null}

      <Table<TeamMealRow>
        rowKey="teamId"
        size="middle"
        columns={columns}
        dataSource={summary.teams}
        pagination={false}
        scroll={{ x: 'max-content' }}
        style={{ marginBottom: 16 }}
      />

      <Button
        type="primary"
        disabled={!canNotifyKitchen}
        loading={markNotified.isPending}
        onClick={() => markNotified.mutate()}
      >
        {MEAL_TEXT.summary.notifyKitchen}
      </Button>
      {summary.status === 'Open' ? (
        <Typography.Text type="secondary" style={{ marginLeft: 12 }}>
          {MEAL_TEXT.summary.notifyDisabledHint}
        </Typography.Text>
      ) : null}
    </div>
  );
}
