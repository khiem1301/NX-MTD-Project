import { Button, Card, Input, Select, Space, Table, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ACCOUNT_STATUS_TAG, EMPLOYEE_STATUS_TAG, MANAGER_HIDDEN_ROLES, PAY_TYPE_LABEL, ROLE_LABEL } from '@/constants/hr';
import { MESSAGES } from '@/constants/messages';
import { ROLES, type Role } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { EmployeeFormDrawer } from '../components/EmployeeFormDrawer';
import { EmployeeImportModal } from '../components/EmployeeImportModal';
import { TempPasswordModal } from '../components/TempPasswordModal';
import { useEmployees } from '../hooks/useEmployees';
import type { EmployeeListItem, TempPassword } from '../types';

type Filters = { keyword?: string; teamId?: number; role?: Role; status?: 'Active' | 'Resigned' };

/** UC16 / UC17: Admin thấy hết và thêm / sửa; Quản đốc không thấy Admin, Quản đốc, Kế toán và chỉ sửa hồ sơ. */
export function EmployeesPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  const base = isAdmin ? ROUTES.adminEmployees : ROUTES.managerEmployees;
  const navigate = useNavigate();
  const table = useTableQuery<Filters>({});
  const employees = useEmployees(table.params);
  const teams = useTeamLookup();
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [tempPassword, setTempPassword] = useState<TempPassword | null>(null);

  const roleOptions = (Object.keys(ROLE_LABEL) as Role[])
    .filter((r) => isAdmin || !MANAGER_HIDDEN_ROLES.includes(r))
    .map((r) => ({ value: r, label: ROLE_LABEL[r] }));

  const columns: TableColumnsType<EmployeeListItem> = [
    {
      title: MESSAGES.hr.employeeCode,
      dataIndex: 'employeeCode',
      render: (v: string, e) => <a onClick={() => navigate(ROUTES.employeeDetail(base, e.employeeId))}>{v}</a>,
    },
    { title: MESSAGES.hr.fullName, dataIndex: 'fullName' },
    { title: MESSAGES.hr.role, dataIndex: 'role', render: (v: Role) => ROLE_LABEL[v] ?? v },
    { title: MESSAGES.hr.team, dataIndex: 'teamName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.payType, dataIndex: 'payType', render: (v: keyof typeof PAY_TYPE_LABEL) => PAY_TYPE_LABEL[v] ?? v },
    { title: MESSAGES.hr.grade, dataIndex: 'currentGradeCode', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.hr.attendanceCode, dataIndex: 'attendanceCode', render: (v: number | null) => v ?? '—' },
    { title: MESSAGES.hr.startDate, dataIndex: 'startDate', render: formatDate },
    { title: MESSAGES.hr.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={EMPLOYEE_STATUS_TAG} /> },
    ...(isAdmin
      ? [{ title: MESSAGES.hr.account, dataIndex: 'accountStatus', render: (v: string | null) => (v ? <StatusTag status={v} labels={ACCOUNT_STATUS_TAG} /> : '—') }]
      : []),
  ];

  return (
    <Card
      title={MESSAGES.hr.employeesTitle}
      extra={
        isAdmin && (
          <Space>
            <Button onClick={() => setImporting(true)}>Nhập từ Excel</Button>
            <Button type="primary" onClick={() => setCreating(true)}>{MESSAGES.hr.addEmployee}</Button>
          </Space>
        )
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search allowClear placeholder={MESSAGES.common.search} onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })} style={{ width: 240 }} />
        <Select<number> allowClear showSearch optionFilterProp="label" placeholder={MESSAGES.hr.team} style={{ width: 200 }}
          options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))} onChange={(teamId) => table.setFilters({ teamId })} />
        <Select<Role> allowClear placeholder={MESSAGES.hr.role} style={{ width: 160 }} options={roleOptions} onChange={(role) => table.setFilters({ role })} />
        <Select<'Active' | 'Resigned'> allowClear placeholder={`${MESSAGES.hr.status}: ${EMPLOYEE_STATUS_TAG.Active?.text}`} style={{ width: 190 }}
          options={[{ value: 'Active', label: EMPLOYEE_STATUS_TAG.Active?.text }, { value: 'Resigned', label: EMPLOYEE_STATUS_TAG.Resigned?.text }]}
          onChange={(status) => table.setFilters({ status })} />
      </Space>
      {isAdmin && <EmployeeImportModal open={importing} onClose={() => setImporting(false)} />}
      <QueryError error={employees.error} onRetry={() => void employees.refetch()} />
      <Table<EmployeeListItem>
        rowKey="employeeId"
        loading={employees.isFetching}
        columns={columns}
        dataSource={employees.data?.items ?? []}
        pagination={table.pagination(employees.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.noData }}
        scroll={{ x: 'max-content' }}
      />
      <EmployeeFormDrawer open={creating} employee={null} isAdmin={isAdmin} onClose={() => setCreating(false)} onCreated={setTempPassword} />
      <TempPasswordModal value={tempPassword} onClose={() => setTempPassword(null)} />
    </Card>
  );
}
