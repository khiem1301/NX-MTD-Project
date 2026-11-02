import { Button, Card, Space, type TableColumnsType } from 'antd';
import { FilterSelect } from '@/components/common/FilterSelect';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import {
  PlusOutlined,
  EditOutlined,
  PlayCircleOutlined,
  StopOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { TableToolbar } from '@/components/common/TableToolbar';
import { TEAM_STATUS_TAG, TEAM_TYPE_LABEL } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { TeamDetailModal } from '../components/TeamDetailModal';
import { TeamFormModal } from '../components/TeamFormModal';
import { useTeams, useTeamStatus } from '../hooks/useTeams';
import type { TeamListItem, TeamStatus } from '../types';
import { DataTable } from '@/components/common/DataTable';

const STATUS_OPTIONS = Object.entries(TEAM_STATUS_TAG).map(([value, label]) => ({
  value: value as TeamStatus,
  label: label.text,
}));

/** UC18: Admin + Quản đốc quản lý tổ. Ngừng tổ còn người bị chặn ở BE (TEAM_HAS_ACTIVE_MEMBERS). */
export function TeamsPage() {
  const [, setParams] = useSearchParams();
  const [statusFilter, setStatusFilter] = useState<TeamStatus>();
  const teams = useTeams();
  const status = useTeamStatus();
  const [editing, setEditing] = useState<TeamListItem | null>(null);
  const [open, setOpen] = useState(false);

  const rows = (teams.data ?? []).filter((t) => !statusFilter || t.status === statusFilter);

  const columns: TableColumnsType<TeamListItem> = [
    {
      title: MESSAGES.hr.teamCode,
      dataIndex: 'teamCode',
    },
    { title: MESSAGES.hr.teamName, dataIndex: 'teamName' },
    {
      title: MESSAGES.hr.teamType,
      dataIndex: 'teamType',
      render: (v: keyof typeof TEAM_TYPE_LABEL) => TEAM_TYPE_LABEL[v] ?? v,
    },
    { title: MESSAGES.hr.leader, dataIndex: 'leaderName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.deputy, dataIndex: 'deputyName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.members, dataIndex: 'memberCount', align: 'right' },
    {
      title: MESSAGES.hr.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={TEAM_STATUS_TAG} />,
    },
    {
      title: MESSAGES.common.actions,
      render: (_: unknown, t) => (
        <Space size={0} wrap>
          <IconAction
            title={MESSAGES.common.view}
            icon={<EyeOutlined />}
            onClick={() => setParams({ id: String(t.teamId) })}
          />
          <IconAction
            title={MESSAGES.common.edit}
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(t);
              setOpen(true);
            }}
          />
          {t.status === 'Active' ? (
            <ConfirmAction
              title={MESSAGES.hr.discontinue}
              onConfirm={() => status.mutate({ id: t.teamId, active: false })}
            >
              <IconAction title={MESSAGES.hr.discontinue} icon={<StopOutlined />} danger />
            </ConfirmAction>
          ) : (
            <IconAction
              title={MESSAGES.hr.reactivate}
              icon={<PlayCircleOutlined />}
              onClick={() => status.mutate({ id: t.teamId, active: true })}
            />
          )}
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={MESSAGES.hr.teamsTitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          {MESSAGES.hr.addTeam}
        </Button>
      }
    >
      <TableToolbar
        filters={
          <FilterSelect<TeamStatus>
            value={statusFilter}
            placeholder={MESSAGES.hr.status}
            options={STATUS_OPTIONS}
            onChange={setStatusFilter}
            style={{ width: 170 }}
          />
        }
      />
      <QueryError error={teams.error} onRetry={() => void teams.refetch()} />
      <DataTable<TeamListItem>
        rowKey="teamId"
        loading={teams.isFetching}
        columns={columns}
        dataSource={rows}
      />
      <TeamFormModal open={open} team={editing} onClose={() => setOpen(false)} />
      <TeamDetailModal />
    </Card>
  );
}
