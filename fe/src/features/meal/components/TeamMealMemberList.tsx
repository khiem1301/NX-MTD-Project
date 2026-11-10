import { Alert, Button, Checkbox, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { formatDateTime } from '@/utils/format';
import { MEAL_TEXT } from '../constants';
import { useConfirmTeamMeal, useSetTeamMemberMeal } from '../hooks/useMeal';
import type { TeamMealMember, TeamMealMembers, TeamMealSuggestion } from '../types';

type Props = {
  date: string;
  members: TeamMealMembers;
  suggestion: TeamMealSuggestion;
};

/** Checklist suất ăn tổ: mặc định tick người đã quẹt; tổ trưởng bỏ tick hộ. */
export function TeamMealMemberList({ date, members, suggestion }: Props) {
  const setMember = useSetTeamMemberMeal(date);
  const confirm = useConfirmTeamMeal(date);

  const sourceKey = suggestion.source ?? 'none';
  const sourceLabel =
    sourceKey === 'Confirmed'
      ? MEAL_TEXT.suggestion.sourceConfirmed
      : sourceKey === 'Attendance'
        ? MEAL_TEXT.suggestion.sourceAttendance
        : MEAL_TEXT.suggestion.sourceNone;

  const columns: ColumnsType<TeamMealMember> = [
    {
      title: MEAL_TEXT.checklist.code,
      dataIndex: 'employeeCode',
      width: 120,
    },
    {
      title: MEAL_TEXT.checklist.name,
      dataIndex: 'fullName',
    },
    {
      title: MEAL_TEXT.checklist.willEat,
      dataIndex: 'willEat',
      width: 120,
      align: 'center',
      render: (willEat: boolean, row) => (
        <Checkbox
          checked={willEat}
          disabled={members.isLocked || setMember.isPending}
          onChange={(e) =>
            setMember.mutate({
              mealDate: date,
              employeeId: row.employeeId,
              willEat: e.target.checked,
            })
          }
        />
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap size={16}>
        <span>
          {MEAL_TEXT.checklist.attendanceCount}: <strong>{members.attendanceCount}</strong>
        </span>
        <span>
          {MEAL_TEXT.checklist.mealCount}: <strong>{members.mealCount}</strong>
        </span>
        <Tag color={sourceKey === 'Confirmed' ? 'green' : sourceKey === 'Attendance' ? 'blue' : 'default'}>
          {sourceLabel}
        </Tag>
        {suggestion.confirmedAt ? (
          <span>
            {MEAL_TEXT.suggestion.confirmedAt}: {formatDateTime(suggestion.confirmedAt)}
          </span>
        ) : null}
      </Space>

      {members.isLocked ? (
        <Alert type="warning" showIcon message={MEAL_TEXT.suggestion.lockedHint} />
      ) : (
        <Alert type="info" showIcon message={MEAL_TEXT.checklist.hint} />
      )}

      <Table
        rowKey="employeeId"
        columns={columns}
        dataSource={members.members}
        pagination={false}
        size="middle"
        locale={{ emptyText: MEAL_TEXT.checklist.empty }}
      />

      <Button
        type="primary"
        loading={confirm.isPending}
        disabled={members.isLocked}
        onClick={() => confirm.mutate({ mealDate: date })}
      >
        {MEAL_TEXT.suggestion.confirm}
      </Button>
    </Space>
  );
}
