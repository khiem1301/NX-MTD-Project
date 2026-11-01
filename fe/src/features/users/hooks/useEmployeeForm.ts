import { Form } from 'antd';
import dayjs from 'dayjs';
import { ADMIN_ASSIGNED_ROLES, suggestPayType, ROLE_LABEL, type PayType } from '@/constants/hr';
import { useTeamLookup } from '@/hooks/useHrLookups';
import type { ApiError } from '@/types/api';
import { toApiDate } from '@/utils/format';
import { useCreateEmployee, useUpdateEmployee } from './useEmployees';
import type { EmployeeFormModalProps } from '../components/EmployeeFormModal';
import { type Dayjs } from 'dayjs';
import { ROLES, type Role } from '@/constants/roles';

/** Trạng thái và xử lý của EmployeeFormModal; component chỉ lo giao diện. */
export function useEmployeeForm(
  props: Pick<EmployeeFormModalProps, 'employee' | 'isAdmin' | 'onClose' | 'onCreated'>,
) {
  const { employee, isAdmin, onClose, onCreated } = props;
  const [form] = Form.useForm<FormValues>();
  const teams = useTeamLookup();
  const create = useCreateEmployee();
  const update = useUpdateEmployee(employee?.employeeId ?? 0);
  const saving = create.isPending || update.isPending;
  // Quản đốc không gán được Admin / Quản đốc / Kế toán; nhân viên đang là Quản đốc thì khóa ô vai trò, hiện đúng vai trò hiện tại.
  const roleLocked = !isAdmin && !!employee && ADMIN_ASSIGNED_ROLES.includes(employee.role);
  const roleOptions =
    isAdmin || roleLocked
      ? ROLE_OPTIONS
      : ROLE_OPTIONS.filter((o) => !ADMIN_ASSIGNED_ROLES.includes(o.value));

  const suggest = () => {
    const values = form.getFieldsValue();
    const teamType = teams.data?.find((t) => t.teamId === values.teamId)?.teamType;
    const pay = suggestPayType(values.role, teamType);
    if (pay && !employee) form.setFieldValue('payType', pay);
  };

  const showFieldErrors = (error: ApiError) =>
    form.setFields(
      Object.entries(error.errors ?? {}).map(([name, errors]) => ({
        name: name as keyof FormValues,
        errors,
      })),
    );

  const handleFinish = (v: FormValues) => {
    const base = {
      fullName: v.fullName,
      gender: v.gender ?? null,
      dateOfBirth: v.dateOfBirth ? toApiDate(v.dateOfBirth) : null,
      phone: v.phone ?? null,
      startDate: toApiDate(v.startDate),
      attendanceCode: v.attendanceCode ?? null,
    };
    if (!employee) {
      create.mutate(
        {
          ...base,
          employeeCode: v.employeeCode,
          role: v.role,
          teamId: v.teamId ?? null,
          payType: v.payType,
        },
        {
          onSuccess: (data) => {
            onCreated({ username: data.username, temporaryPassword: data.temporaryPassword });
            onClose();
          },
          onError: showFieldErrors,
        },
      );
      return;
    }
    const body = {
      ...base,
      role: v.role,
      teamId: v.teamId ?? null,
      payType: v.payType,
      rowVer: employee.rowVer,
    };
    update.mutate(body, { onSuccess: onClose, onError: showFieldErrors });
  };

  const initialValues: Partial<FormValues> = employee
    ? {
        employeeCode: employee.employeeCode,
        fullName: employee.fullName,
        gender: employee.gender ?? undefined,
        dateOfBirth: employee.dateOfBirth ? dayjs(employee.dateOfBirth) : undefined,
        phone: employee.phone ?? undefined,
        role: employee.role,
        teamId: employee.teamId ?? undefined,
        payType: employee.payType,
        startDate: dayjs(employee.startDate),
        attendanceCode: employee.attendanceCode ?? undefined,
      }
    : { startDate: dayjs(), payType: 'Monthly' };

  return {
    teams,
    saving,
    roleOptions,
    roleLocked,
    suggest,
    handleFinish,
    initialValues,
    form,
  };
}

type FormValues = {
  employeeCode: string;
  fullName: string;
  gender?: string;
  dateOfBirth?: Dayjs;
  phone?: string;
  role: Role;
  teamId?: number;
  payType: PayType;
  startDate: Dayjs;
  attendanceCode?: number;
};

const ROLE_OPTIONS = (Object.values(ROLES) as Role[]).map((r) => ({
  value: r,
  label: ROLE_LABEL[r],
}));
