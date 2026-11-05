import { Button, Result, Space, Spin, Tabs, Tag, Typography, type TableColumnsType } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AuditHistoryTable } from '@/components/common/AuditHistoryTable';
import { QueryError } from '@/components/common/QueryError';
import { PAY_TYPE_LABEL, ROLE_LABEL } from '@/constants/hr';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { fill } from '@/utils/format';
import { useRemoveMembers, useTeam } from '../hooks/useTeams';
import { APPOINTABLE_ROLES, isLeadership } from '../utils/teamRoles';

import type { TeamDetail, TeamMember } from '../types';
import { AddMembersModal } from './AddMembersModal';
import { TeamMemberActions } from './TeamMemberActions';
import { TransferModal } from './TransferModal';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

const nameOf = (team: TeamDetail, role: string) =>
  team.members.find((m) => m.role === role)?.fullName ?? MESSAGES.hr.noLeader;

/**
 * Chi tiết tổ mở theo query ?id=: thành viên, thêm / loại nhân viên, chuyển tổ (D32),
 * bổ nhiệm và thôi giữ chức tổ trưởng / tổ phó (D33), lịch sử.
 */
export function TeamDetailModal() {
  const [params, setParams] = useSearchParams();
  const idParam = Number(params.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error: queryError, refetch } = useTeam(id);
  const error = queryError as ApiError | null;
  const remove = useRemoveMembers();
  const [selected, setSelected] = useState<number[]>([]);
  const [transferring, setTransferring] = useState(false);
  const [adding, setAdding] = useState(false);

  const isActive = data?.status === 'Active';
  const canAppoint = data !== undefined && data.teamType !== 'Office' && isActive;

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
        data && canAppoint && APPOINTABLE_ROLES.includes(m.role) ? (
          <TeamMemberActions team={data} member={m} />
        ) : null,
    },
  ];

  const close = () => {
    setSelected([]);
    setParams({});
  };

  const handleRemove = () =>
    remove.mutate(
      { id: data?.teamId ?? 0, body: { employeeIds: selected } },
      { onSuccess: () => setSelected([]) },
    );

  return (
    <DetailModal
      open={id !== null}
      onClose={close}
      title={data ? `${data.teamCode} · ${data.teamName}` : undefined}
      extra={
        data && (
          <Space wrap>
            <Button disabled={!isActive} onClick={() => setAdding(true)}>
              {MESSAGES.hr.addMembers}
            </Button>
            <ConfirmAction
              title={fill(MESSAGES.hr.removeConfirm, String(selected.length))}
              disabled={selected.length === 0}
              onConfirm={handleRemove}
            >
              <Button danger disabled={selected.length === 0}>
                {MESSAGES.hr.removeMembers}
              </Button>
            </ConfirmAction>
            <Button
              disabled={selected.length === 0 || !isActive}
              onClick={() => setTransferring(true)}
            >
              {MESSAGES.hr.transfer}
            </Button>
          </Space>
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
              label: `${MESSAGES.hr.members} (${data.members.length})`,
              children: (
                <>
                  <Space wrap style={{ marginBottom: 8 }}>
                    <Tag>
                      {MESSAGES.hr.leader}: {nameOf(data, 'TeamLeader')}
                    </Tag>
                    <Tag>
                      {MESSAGES.hr.deputy}: {nameOf(data, 'TeamDeputy')}
                    </Tag>
                  </Space>
                  <DataTable<TeamMember>
                    rowKey="employeeId"
                    size="small"
                    pagination={false}
                    columns={columns}
                    dataSource={data.members}
                    rowSelection={{
                      selectedRowKeys: selected,
                      onChange: (keys) => setSelected(keys as number[]),
                      getCheckboxProps: (m) => ({ disabled: isLeadership(m) }),
                    }}
                  />
                  <Typography.Text type="secondary">
                    {MESSAGES.hr.leadersMustResign}
                  </Typography.Text>
                </>
              ),
            },
            {
              key: 'history',
              label: MESSAGES.hr.history,
              children: <AuditHistoryTable history={data.history} />,
            },
          ]}
        />
      )}
      {data && (
        <>
          <AddMembersModal open={adding} teamId={data.teamId} onClose={() => setAdding(false)} />
          <TransferModal
            open={transferring}
            fromTeamId={data.teamId}
            employeeIds={selected}
            onClose={() => {
              setTransferring(false);
              setSelected([]);
            }}
          />
        </>
      )}
    </DetailModal>
  );
}
