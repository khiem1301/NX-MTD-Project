import { Button, Popconfirm, Result, Space, Spin, Tabs, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AuditHistory } from '@/components/common/AuditHistory';
import { QueryError } from '@/components/common/QueryError';
import { PAY_TYPE_LABEL, ROLE_LABEL } from '@/constants/hr';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { fill } from '@/utils/format';
import { useAppoint, useTeam } from '../hooks/useTeams';
import type { AppointPosition, TeamMember } from '../types';
import { TransferModal } from './TransferModal';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

const APPOINTABLE_ROLES = ['Worker', 'TeamLeader', 'TeamDeputy'];
const POSITION_ROLE: Record<AppointPosition, string> = {
  Leader: 'TeamLeader',
  Deputy: 'TeamDeputy',
};
const POSITION_LABEL: Record<AppointPosition, string> = { Leader: 'tổ trưởng', Deputy: 'tổ phó' };

/** Chi tiết tổ mở theo query ?id=: thành viên, bổ nhiệm tổ trưởng / tổ phó (D33), chuyển tổ (D32), lịch sử. */
export function TeamDetailModal() {
  const [params, setParams] = useSearchParams();
  const idParam = Number(params.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error: queryError, refetch } = useTeam(id);
  const error = queryError as ApiError | null;
  const appoint = useAppoint();
  const [selected, setSelected] = useState<number[]>([]);
  const [transferring, setTransferring] = useState(false);

  const canAppoint = data !== undefined && data.teamType !== 'Office' && data.status === 'Active';
  const holderOf = (position: AppointPosition, exceptId: number): TeamMember | undefined =>
    data?.members.find((m) => m.role === POSITION_ROLE[position] && m.employeeId !== exceptId);

  const appointButton = (m: TeamMember, position: AppointPosition) => {
    if (m.role === POSITION_ROLE[position]) return null;
    const holder = holderOf(position, m.employeeId);
    const label = position === 'Leader' ? MESSAGES.hr.appointLeader : MESSAGES.hr.appointDeputy;
    const run = () => appoint.mutate({ id: data?.teamId ?? 0, employeeId: m.employeeId, position });
    return holder ? (
      <Popconfirm
        title={fill(MESSAGES.hr.appointConfirm, holder.fullName, POSITION_LABEL[position])}
        onConfirm={run}
      >
        <Button size="small">{label}</Button>
      </Popconfirm>
    ) : (
      <Button size="small" onClick={run}>
        {label}
      </Button>
    );
  };

  const columns: TableColumnsType<TeamMember> = [
    { title: MESSAGES.hr.employeeCode, dataIndex: 'employeeCode' },
    { title: MESSAGES.hr.fullName, dataIndex: 'fullName' },
    {
      title: MESSAGES.hr.role,
      dataIndex: 'role',
      render: (v: keyof typeof ROLE_LABEL) => ROLE_LABEL[v] ?? v,
    },
    {
      title: MESSAGES.hr.payType,
      dataIndex: 'payType',
      render: (v: keyof typeof PAY_TYPE_LABEL) => PAY_TYPE_LABEL[v] ?? v,
    },
    {
      title: MESSAGES.hr.grade,
      dataIndex: 'currentGradeCode',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, m) =>
        canAppoint && APPOINTABLE_ROLES.includes(m.role) ? (
          <Space>
            {appointButton(m, 'Leader')}
            {appointButton(m, 'Deputy')}
          </Space>
        ) : null,
    },
  ];

  const close = () => {
    setSelected([]);
    setParams({});
  };

  return (
    <DetailModal
      open={id !== null}
      onClose={close}
      title={data ? `${data.teamCode} · ${data.teamName}` : undefined}
      extra={
        data && (
          <Button
            disabled={selected.length === 0 || data.status !== 'Active'}
            onClick={() => setTransferring(true)}
          >
            {MESSAGES.hr.transfer}
          </Button>
        )
      }
    >
      {isLoading && <Spin />}
      {error?.errorCode === ERROR_CODES.CommonNotFound && (
        <Result status="404" subTitle={MESSAGES.common.notFound} />
      )}
      {error && error.errorCode !== ERROR_CODES.CommonNotFound && (
        <QueryError error={error} onRetry={() => void refetch()} />
      )}
      {data && (
        <Tabs
          items={[
            {
              key: 'members',
              label: MESSAGES.hr.members,
              children: (
                <DataTable<TeamMember>
                  rowKey="employeeId"
                  size="small"
                  pagination={false}
                  columns={columns}
                  dataSource={data.members}
                  rowSelection={{
                    selectedRowKeys: selected,
                    onChange: (keys) => setSelected(keys as number[]),
                  }}
                />
              ),
            },
            {
              key: 'history',
              label: MESSAGES.hr.history,
              children: <AuditHistory history={data.history} />,
            },
          ]}
        />
      )}
      {data && (
        <TransferModal
          open={transferring}
          fromTeamId={data.teamId}
          employeeIds={selected}
          onClose={() => {
            setTransferring(false);
            setSelected([]);
          }}
        />
      )}
    </DetailModal>
  );
}
