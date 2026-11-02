import { Space } from 'antd';
import { CrownOutlined, UserDeleteOutlined, UserSwitchOutlined } from '@ant-design/icons';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { useAppoint, useDismiss } from '../hooks/useTeams';
import { isLeadership } from '../teamRoles';
import type { AppointPosition, TeamDetail, TeamMember } from '../types';

const POSITION_ROLE: Record<AppointPosition, string> = {
  Leader: 'TeamLeader',
  Deputy: 'TeamDeputy',
};
const POSITION_LABEL: Record<AppointPosition, string> = { Leader: 'tổ trưởng', Deputy: 'tổ phó' };

type TeamMemberActionsProps = { team: TeamDetail; member: TeamMember };

/** Bổ nhiệm tổ trưởng / tổ phó (D33) và thôi giữ chức cho một thành viên của tổ. */
export function TeamMemberActions({ team, member }: TeamMemberActionsProps) {
  const appoint = useAppoint();
  const dismiss = useDismiss();

  const appointButton = (position: AppointPosition) => {
    if (member.role === POSITION_ROLE[position]) return null;
    const holder = team.members.find(
      (m) => m.role === POSITION_ROLE[position] && m.employeeId !== member.employeeId,
    );
    const label = position === 'Leader' ? MESSAGES.hr.appointLeader : MESSAGES.hr.appointDeputy;
    const icon = position === 'Leader' ? <CrownOutlined /> : <UserSwitchOutlined />;
    const run = () => appoint.mutate({ id: team.teamId, employeeId: member.employeeId, position });
    return holder ? (
      <ConfirmAction
        title={fill(MESSAGES.hr.appointConfirm, holder.fullName, POSITION_LABEL[position])}
        onConfirm={run}
      >
        <IconAction title={label} icon={icon} />
      </ConfirmAction>
    ) : (
      <IconAction title={label} icon={icon} onClick={run} />
    );
  };

  return (
    <Space size={0}>
      {appointButton('Leader')}
      {appointButton('Deputy')}
      {isLeadership(member) && (
        <ConfirmAction
          title={fill(MESSAGES.hr.dismissConfirm, member.fullName)}
          onConfirm={() => dismiss.mutate({ id: team.teamId, employeeId: member.employeeId })}
        >
          <IconAction title={MESSAGES.hr.dismiss} icon={<UserDeleteOutlined />} danger />
        </ConfirmAction>
      )}
    </Space>
  );
}
