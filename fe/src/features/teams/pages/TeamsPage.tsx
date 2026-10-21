import { Button, Card, Popconfirm, Space, Switch, Table, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { TEAM_STATUS_TAG, TEAM_TYPE_LABEL } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { TeamDetailDrawer } from '../components/TeamDetailDrawer';
import { TeamFormModal } from '../components/TeamFormModal';
import { useTeams, useTeamStatus } from '../hooks/useTeams';
import type { TeamListItem } from '../types';

/** UC18: Admin + Quản đốc quản lý tổ. Ngừng tổ còn người bị chặn ở BE (TEAM_HAS_ACTIVE_MEMBERS). */
export function TeamsPage() {
  const [, setParams] = useSearchParams();
  const [showDiscontinued, setShowDiscontinued] = useState(false);
  const teams = useTeams(showDiscontinued);
  const status = useTeamStatus();
  const [editing, setEditing] = useState<TeamListItem | null>(null);
  const [open, setOpen] = useState(false);

  const columns: TableColumnsType<TeamListItem> = [
    {
      title: MESSAGES.hr.teamCode,
      dataIndex: 'teamCode',
      render: (v: string, t) => <a onClick={() => setParams({ id: String(t.teamId) })}>{v}</a>,
    },
    { title: MESSAGES.hr.teamName, dataIndex: 'teamName' },
    { title: MESSAGES.hr.teamType, dataIndex: 'teamType', render: (v: keyof typeof TEAM_TYPE_LABEL) => TEAM_TYPE_LABEL[v] ?? v },
    { title: MESSAGES.hr.leader, dataIndex: 'leaderName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.deputy, dataIndex: 'deputyName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.members, dataIndex: 'memberCount', align: 'right' },
    { title: MESSAGES.hr.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={TEAM_STATUS_TAG} /> },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, t) => (
        <Space>
          <Button size="small" onClick={() => { setEditing(t); setOpen(true); }}>{MESSAGES.common.edit}</Button>
          {t.status === 'Active' ? (
            <Popconfirm title={MESSAGES.hr.discontinue} onConfirm={() => status.mutate({ id: t.teamId, active: false })}>
              <Button size="small" danger>{MESSAGES.hr.discontinue}</Button>
            </Popconfirm>
          ) : (
            <Button size="small" onClick={() => status.mutate({ id: t.teamId, active: true })}>{MESSAGES.hr.reactivate}</Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.hr.teamsTitle}
      extra={<Button type="primary" onClick={() => { setEditing(null); setOpen(true); }}>{MESSAGES.hr.addTeam}</Button>}
    >
      <Space style={{ marginBottom: 16 }}>
        <Switch checked={showDiscontinued} onChange={setShowDiscontinued} />
        {MESSAGES.hr.showDiscontinued}
      </Space>
      <QueryError error={teams.error} onRetry={() => void teams.refetch()} />
      <Table<TeamListItem>
        rowKey="teamId"
        loading={teams.isFetching}
        columns={columns}
        dataSource={teams.data ?? []}
        pagination={false}
        locale={{ emptyText: MESSAGES.common.noData }}
      />
      <TeamFormModal open={open} team={editing} onClose={() => setOpen(false)} />
      <TeamDetailDrawer />
    </Card>
  );
}
