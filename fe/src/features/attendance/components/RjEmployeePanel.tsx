import { useState } from 'react';
import { Alert, Button, Checkbox, Empty, Modal, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SyncOutlined, UserAddOutlined } from '@ant-design/icons';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { ROLES } from '@/constants/roles';
import { TempPasswordModal } from '@/features/users/components/TempPasswordModal';
import type { TempPassword } from '@/features/users/types';
import { useAuth } from '@/hooks/useAuth';
import { formatDateTime } from '@/utils/format';
import { ATTENDANCE_TEXT } from '../constants';
import {
  useImportEmployeeFromRj,
  useRequestRjEmployeeSync,
  useRjEmployees,
} from '../hooks/useAttendance';
import type { RjEmployee } from '../types';

/** Danh sách NV từ RJ: mọi role được vào màn có thể xem + đồng bộ; chỉ Admin / Quản đốc cấp tài khoản. */
export function RjEmployeePanel() {
  const { user } = useAuth();
  const canProvision =
    user?.role === ROLES.Admin || user?.role === ROLES.Manager;
  const [onlyUnmapped, setOnlyUnmapped] = useState(true);
  const { data, isLoading, error, refetch, isFetching } = useRjEmployees(onlyUnmapped);
  const importFromRj = useImportEmployeeFromRj();
  const syncFromRj = useRequestRjEmployeeSync();
  const [tempPassword, setTempPassword] = useState<TempPassword | null>(null);

  if (error) {
    return <Alert type="error" showIcon message={(error as { message: string }).message} />;
  }

  const columns: ColumnsType<RjEmployee> = [
    {
      title: ATTENDANCE_TEXT.table.attendanceCode,
      dataIndex: 'attendanceCode',
      width: 120,
      render: (v: number) => <Tag color="blue">{v}</Tag>,
    },
    {
      title: ATTENDANCE_TEXT.rj.rjCode,
      dataIndex: 'rjEmployeeCode',
      width: 120,
      render: (v: string | null) => v ?? '—',
    },
    {
      title: ATTENDANCE_TEXT.table.employee,
      dataIndex: 'fullName',
    },
    {
      title: ATTENDANCE_TEXT.table.status,
      key: 'mapped',
      width: 220,
      render: (_, row) =>
        row.isMapped ? (
          <Tag color="green">
            {ATTENDANCE_TEXT.rj.mappedTo}: {row.employeeCode} — {row.employeeFullName}
          </Tag>
        ) : (
          <Tag>{ATTENDANCE_TEXT.rj.unmapped}</Tag>
        ),
    },
    {
      title: ATTENDANCE_TEXT.rj.syncedAt,
      dataIndex: 'syncedAt',
      responsive: ['lg'],
      render: (v: string) => formatDateTime(v),
    },
    ...(canProvision
      ? [
          {
            title: ATTENDANCE_TEXT.table.action,
            key: 'actions',
            width: 100,
            render: (_: unknown, row: RjEmployee) =>
              row.isMapped ? null : (
                <IconAction
                  title={ATTENDANCE_TEXT.rj.import}
                  icon={<UserAddOutlined />}
                  onClick={() => {
                    Modal.confirm({
                      title: ATTENDANCE_TEXT.rj.import,
                      content: ATTENDANCE_TEXT.rj.importConfirm
                        .replace('{0}', row.fullName)
                        .replace('{1}', String(row.attendanceCode)),
                      okText: ATTENDANCE_TEXT.rj.import,
                      onOk: () =>
                        importFromRj
                          .mutateAsync({ attendanceCode: row.attendanceCode })
                          .then((created) => {
                            setTempPassword({
                              username: created.username,
                              temporaryPassword: created.temporaryPassword,
                            });
                          }),
                    });
                  }}
                />
              ),
          } satisfies ColumnsType<RjEmployee>[number],
        ]
      : []),
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert type="info" showIcon message={ATTENDANCE_TEXT.rj.hint} />
      <Space wrap>
        <Button
          type="primary"
          icon={<SyncOutlined spin={syncFromRj.isPending} />}
          loading={syncFromRj.isPending}
          onClick={() => syncFromRj.mutate()}
        >
          {ATTENDANCE_TEXT.rj.sync}
        </Button>
        <Button onClick={() => void refetch()} loading={isFetching}>
          Làm mới danh sách
        </Button>
        <Typography.Text type="secondary">{ATTENDANCE_TEXT.rj.syncHint}</Typography.Text>
      </Space>
      <Checkbox checked={onlyUnmapped} onChange={(e) => setOnlyUnmapped(e.target.checked)}>
        {ATTENDANCE_TEXT.rj.onlyUnmapped}
      </Checkbox>

      <DataTable<RjEmployee>
        rowKey="attendanceCode"
        size="small"
        columns={columns}
        dataSource={data ?? []}
        loading={isLoading || importFromRj.isPending}
        pagination={{ pageSize: 50, showSizeChanger: true }}
        scroll={{ x: 'max-content' }}
        locale={{
          emptyText: (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={ATTENDANCE_TEXT.rj.empty} />
          ),
        }}
      />

      {(data?.length ?? 0) > 0 ? (
        <Typography.Text type="secondary">
          {data?.length} người — {ATTENDANCE_TEXT.rj.syncedAt}:{' '}
          {data?.[0]?.syncedAt ? formatDateTime(data[0].syncedAt) : '—'}
        </Typography.Text>
      ) : null}

      <TempPasswordModal value={tempPassword} onClose={() => setTempPassword(null)} />
    </Space>
  );
}
