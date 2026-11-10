import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App } from 'antd';
import { employeeApi } from '@/features/users/api/employeeApi';
import { useRolePath } from '@/hooks/useRolePath';
import type { ApiError } from '@/types/api';
import { formatDateTime } from '@/utils/format';
import { attendanceApi, type TimesheetAudience } from '../api/attendanceApi';
import { ATTENDANCE_TEXT } from '../constants';
import type {
  AdjustmentFilter,
  CreateAdjustmentPayload,
  AttendanceExcelPreview,
  AttendanceImportResult,
  MapAttendanceCodeRequest,
  TimesheetFilter,
  SyncStatus,
} from '../types';

/** Khóa query gom một nơi để invalidate sau mutation không bị sót. */
export const attendanceKeys = {
  all: ['attendance'] as const,
  timesheets: (audience: TimesheetAudience, filter: TimesheetFilter) =>
    [...attendanceKeys.all, 'timesheets', audience, filter] as const,
  summary: (audience: TimesheetAudience, filter: TimesheetFilter) =>
    [...attendanceKeys.all, 'summary', audience, filter] as const,
  syncHealth: () => [...attendanceKeys.all, 'sync-health'] as const,
  syncSessions: (take: number, status?: SyncStatus) =>
    [...attendanceKeys.all, 'sync-sessions', take, status] as const,
  unmappedCodes: () => [...attendanceKeys.all, 'unmapped-codes'] as const,
  rjEmployees: (onlyUnmapped: boolean) =>
    [...attendanceKeys.all, 'rj-employees', onlyUnmapped] as const,
};

/** Agent đẩy dữ liệu mỗi 10 phút nên làm mới nền ở nhịp ngắn hơn một chút. */
const SYNC_POLL_MS = 60_000;

export const useTimesheets = (audience: TimesheetAudience, filter: TimesheetFilter) =>
  useQuery({
    queryKey: attendanceKeys.timesheets(audience, filter),
    queryFn: () => attendanceApi.getTimesheets(audience, filter),
    placeholderData: (previous) => previous,
  });

export const useTimesheetSummary = (audience: TimesheetAudience, filter: TimesheetFilter) =>
  useQuery({
    queryKey: attendanceKeys.summary(audience, filter),
    queryFn: () => attendanceApi.getSummary(audience, filter),
    placeholderData: (previous) => previous,
  });

export const useSyncHealth = () =>
  useQuery({
    queryKey: attendanceKeys.syncHealth(),
    queryFn: attendanceApi.getSyncHealth,
    refetchInterval: SYNC_POLL_MS,
  });

export const useSyncSessions = (status?: SyncStatus, take = 20) =>
  useQuery({
    queryKey: attendanceKeys.syncSessions(take, status),
    queryFn: () => attendanceApi.getSyncSessions(take, status),
  });

export const useUnmappedCodes = () =>
  useQuery({
    queryKey: attendanceKeys.unmappedCodes(),
    queryFn: attendanceApi.getUnmappedCodes,
  });

export const useRjEmployees = (onlyUnmapped: boolean, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.rjEmployees(onlyUnmapped),
    queryFn: () => attendanceApi.getRjEmployees(onlyUnmapped),
    enabled,
    refetchInterval: SYNC_POLL_MS,
  });

/** Dùng trên form Nhân sự (Admin/Quản đốc) qua API common. */
export const useRjEmployeesCommon = (onlyUnmapped: boolean, enabled = true) =>
  useQuery({
    queryKey: [...attendanceKeys.all, 'rj-employees-common', onlyUnmapped] as const,
    queryFn: () => attendanceApi.getRjEmployeesCommon(onlyUnmapped),
    enabled,
  });

/** Gán mã xong thì bảng công và số liệu đổi theo, nên làm mới cả nhánh attendance. */
export const useMapAttendanceCode = () => {
  const queryClient = useQueryClient();
  const { message } = App.useApp();

  return useMutation<void, ApiError, MapAttendanceCodeRequest>({
    mutationFn: attendanceApi.mapCode,
    onSuccess: async () => {
      message.success(ATTENDANCE_TEXT.map.success);
      await queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
    },
    onError: (error) => {
      message.error(error.message);
    },
  });
};

/** Yêu cầu agent đồng bộ snapshot NV từ RJ; toast tạm, không gắn banner xanh cố định. */
export const useRequestRjEmployeeSync = () => {
  const queryClient = useQueryClient();
  const { message } = App.useApp();

  return useMutation<void, ApiError, void>({
    mutationFn: attendanceApi.requestRjEmployeeSync,
    onSuccess: async () => {
      message.success(ATTENDANCE_TEXT.rj.syncSuccess);
      // Agent thường phản hồi trong ~10s; làm mới rồi báo trạng thái bình thường (toast).
      window.setTimeout(() => {
        void (async () => {
          await queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
          const health = await attendanceApi.getSyncHealth();
          if (!health.isOverdue && health.lastSucceededAt) {
            message.success(
              ATTENDANCE_TEXT.rj.syncHealthyToast.replace(
                '{0}',
                formatDateTime(health.lastSucceededAt),
              ),
            );
          }
        })();
      }, 12_000);
      for (const ms of [3_000, 8_000]) {
        window.setTimeout(() => {
          void queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
        }, ms);
      }
    },
    onError: (error) => {
      message.error(error.message);
    },
  });
};

/** Import công nhân từ RJ — chỉ Admin / Quản đốc (API Nhân viên). */
export const useImportEmployeeFromRj = () => {
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const role = useRolePath();

  return useMutation<
    { employeeId: number; username: string; temporaryPassword: string },
    ApiError,
    { attendanceCode: number; teamId?: number | null; payType?: string | null }
  >({
    mutationFn: (body) => employeeApi.importFromRj(role, body),
    onSuccess: async () => {
      message.success(ATTENDANCE_TEXT.rj.importSuccess);
      await queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
      await queryClient.invalidateQueries({ queryKey: ['employees'] });
    },
    onError: (error) => {
      message.error(error.message);
    },
  });
};

// ---------- Import Excel dự phòng (UC29) ----------

export const usePreviewExcelImport = () => {
  const { message } = App.useApp();
  return useMutation<AttendanceExcelPreview, ApiError, File>({
    mutationFn: attendanceApi.previewExcelImport,
    onError: (error) => message.error(error.message),
  });
};

/** Nhập xong thì bảng công / lịch sử đồng bộ đổi theo nên làm mới cả nhánh attendance (MSG24). */
export const useConfirmExcelImport = () => {
  const queryClient = useQueryClient();
  const { message } = App.useApp();

  return useMutation<AttendanceImportResult, ApiError, File>({
    mutationFn: attendanceApi.confirmExcelImport,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
      message.success(
        `${ATTENDANCE_TEXT.import.successPrefix} ${result.inserted} dòng mới, ` +
          `${result.duplicates} trùng, ${result.skippedLockedMonth} thuộc tháng khóa, ${result.unmapped} chưa gán mã.`,
      );
    },
    onError: (error) => message.error(error.message),
  });
};

// ---------- Chấm công hộ (GBR-25) ----------

export const useAdjustments = (filter: AdjustmentFilter) =>
  useQuery({
    queryKey: [...attendanceKeys.all, 'adjustments', filter] as const,
    queryFn: () => attendanceApi.getAdjustments(filter),
    placeholderData: (previous) => previous,
  });

/** Chấm hộ xong thì bảng công và số liệu đổi theo, nên làm mới cả nhánh attendance. */
export const useCreateAdjustment = () => {
  const queryClient = useQueryClient();
  const { message } = App.useApp();

  return useMutation<void, ApiError, CreateAdjustmentPayload>({
    mutationFn: attendanceApi.createAdjustment,
    onSuccess: async () => {
      message.success(ATTENDANCE_TEXT.adjust.saved);
      await queryClient.invalidateQueries({ queryKey: attendanceKeys.all });
    },
    onError: (error) => {
      message.error(error.message);
    },
  });
};
