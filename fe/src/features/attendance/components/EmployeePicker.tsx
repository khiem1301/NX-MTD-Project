import { useState } from 'react';
import { Select, Space } from 'antd';
import { useTeamLookup, useTeamMemberLookup } from '@/hooks/useHrLookups';
import { ATTENDANCE_TEXT } from '../constants';

type Props = {
  value?: number;
  onChange?: (employeeId: number | undefined) => void;
  disabled?: boolean;
};

/**
 * Chọn nhân viên theo hai bước: tổ rồi nhân viên trong tổ (API tra cứu nhân sự dùng chung). Dùng cho gán mã chấm công và
 * chấm công hộ, thay cho việc gõ tay mã nhân viên.
 */
export function EmployeePicker({ value, onChange, disabled }: Props) {
  const [teamId, setTeamId] = useState<number | null>(null);
  const teams = useTeamLookup();
  const members = useTeamMemberLookup(teamId);

  return (
    <Space.Compact style={{ width: '100%' }}>
      <Select<number>
        style={{ width: '40%' }}
        placeholder={ATTENDANCE_TEXT.adjust.team}
        loading={teams.isLoading}
        disabled={disabled}
        value={teamId ?? undefined}
        options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))}
        onChange={(next) => {
          setTeamId(next);
          onChange?.(undefined);
        }}
      />
      <Select<number>
        showSearch
        style={{ width: '60%' }}
        optionFilterProp="label"
        placeholder={ATTENDANCE_TEXT.adjust.employee}
        loading={members.isLoading}
        disabled={disabled || teamId === null}
        value={value}
        options={(members.data ?? []).map((m) => ({
          value: m.employeeId,
          label: `${m.employeeCode} - ${m.fullName}`,
        }))}
        onChange={onChange}
      />
    </Space.Compact>
  );
}
