import { Button, Card, Space, Table, type TableColumnsType } from 'antd';
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
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { keyword?: string; teamId?: number; role?: Role; status?: 'Active' | 'Resigned' };

/** UC16 / UC17: Admin thấy hết; Quản đốc quản lý mọi người trừ Admin và Kế toán (thêm, sửa, nhập Excel, tài khoản). */
export function EmployeesPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  const canManage = isAdmin || user?.role === ROLES.Manager;
  const base = isAdmin ? ROUTES.adminEmployees : ROUTES.managerEmployees;
  const navigate = useNavigate();
  const table = useTableQuery<Filters>({});
  // Chưa chọn trạng thái = Tất cả (BE mặc định chỉ lấy nhân viên đang làm nên phải gửi All).
  const employees = useEmployees({ ...table.params, status: table.params.status ?? 'All' });
  const teams = useTeamLookup();
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [tempPassword, setTempPassword] = useState<TempPassword | null>(null);

  const roleOptions = (Object.keys(ROLE_LABEL) as Role[])
    .filter((r) => isAdmin || !MANAGER_HIDDEN_ROLES.includes(r))
    .map((r) => ({ value: r, label: ROLE_LABEL[r] }));

  const columns: TableColumnsType<EmployeeListItem> = [
    { title: 'STT', key: 'stt', width: 64, render: (_: unknown, __: EmployeeListItem, index: number) => (table.params.page - 1) * table.params.pageSize + index + 1 },
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
    ...(canManage
      ? [{ title: MESSAGES.hr.account, dataIndex: 'accountStatus', render: (v: string | null) => (v ? <StatusTag status={v} labels={ACCOUNT_STATUS_TAG} /> : '—') }]
      : []),
  ];

  return (
    <Card
      title={MESSAGES.hr.employeesTitle}
      extra={
        canManage && (
          <Space>
            <Button onClick={() => setImporting(true)}>Nhập từ Excel</Button>
            <Button type="primary" onClick={() => setCreating(true)}>{MESSAGES.hr.addEmployee}</Button>
          </Space>
        )
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch allowClear placeholder={MESSAGES.common.search} onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })} style={{ width: 240 }} />
        <FilterSelect<number> value={table.filters.teamId} showSearch optionFilterProp="label" placeholder={MESSAGES.hr.team} style={{ width: 200 }}
          options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))} onChange={(teamId) => table.setFilters({ teamId })} />
        <FilterSelect<Role> value={table.filters.role} placeholder={MESSAGES.hr.role} style={{ width: 160 }} options={roleOptions} onChange={(role) => table.setFilters({ role })} />
        <FilterSelect<'Active' | 'Resigned'> value={table.filters.status} placeholder={MESSAGES.hr.status} style={{ width: 190 }}
          options={[{ value: 'Active', label: EMPLOYEE_STATUS_TAG.Active?.text }, { value: 'Resigned', label: EMPLOYEE_STATUS_TAG.Resigned?.text }]}
          onChange={(status) => table.setFilters({ status })} />
      </Space>
      {canManage && <EmployeeImportModal open={importing} onClose={() => setImporting(false)} />}
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
