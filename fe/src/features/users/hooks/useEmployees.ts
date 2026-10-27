import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { employeeApi } from '../api/employeeApi';
import type { AdminUpdatePayload, CreateEmployeePayload, EmployeeQuery, ManagerUpdatePayload } from '../types';

export const EMPLOYEES_KEY = ['employees'] as const;
/** Đổi nhân viên / tổ thì tra cứu tổ và thành viên của luồng khác cũng phải tải lại. */
const HR_LOOKUPS_KEY = ['hr-lookups'] as const;
const TEAMS_KEY = ['teams'] as const;

export const useEmployees = (query: EmployeeQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...EMPLOYEES_KEY, 'list', role, query],
    queryFn: () => employeeApi.list(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useEmployee = (id: number | null) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...EMPLOYEES_KEY, 'detail', role, id],
    queryFn: () => employeeApi.get(role, id as number),
    enabled: id !== null,
  });
};

const invalidate = [EMPLOYEES_KEY, TEAMS_KEY, HR_LOOKUPS_KEY];

export const useCreateEmployee = () =>
  useApiMutation((body: CreateEmployeePayload) => employeeApi.create(body), {
    invalidate,
    successMessage: MESSAGES.hr.employeeCreated,
  });

/** Admin sửa đủ trường; Quản đốc chỉ sửa thông tin cá nhân + mã chấm công. */
export const useUpdateEmployee = (id: number) => {
  const role = useRolePath();
  return useApiMutation(
    (body: AdminUpdatePayload | ManagerUpdatePayload) =>
      role === 'admin' ? employeeApi.adminUpdate(id, body as AdminUpdatePayload) : employeeApi.managerUpdate(id, body as ManagerUpdatePayload),
    { invalidate, successMessage: MESSAGES.common.saved },
  );
};

export const useResignEmployee = (id: number) =>
  useApiMutation((body: { resignDate: string; reason: string; rowVer: string }) => employeeApi.resign(id, body), { invalidate });

export const useLockAccount = (id: number) =>
  useApiMutation((reason: string) => employeeApi.lock(id, reason), { invalidate });

export const useUnlockAccount = (id: number) => useApiMutation(() => employeeApi.unlock(id), { invalidate });

export const useResetPassword = (id: number) => useApiMutation(() => employeeApi.resetPassword(id), { invalidate });
