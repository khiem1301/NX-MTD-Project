import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App } from 'antd';
import type { ApiError } from '@/types/api';
import { attendanceApi, type TimesheetAudience } from '../api/attendanceApi';
import { ATTENDANCE_TEXT } from '../constants';
import type {
  AdjustmentFilter,
  CreateAdjustmentPayload,
  AttendanceExcelPreview,
  AttendanceImportResult,
  MapAttendanceCodeRequest,
  TimesheetFilter,
} from '../types';

/** Khóa query gom một nơi để invalidate sau mutation không bị sót. */
export const attendanceKeys = {
  all: ['attendance'] as const,
  timesheets: (audience: TimesheetAudience, filter: TimesheetFilter) =>
    [...attendanceKeys.all, 'timesheets', audience, filter] as const,
  summary: (audience: TimesheetAudience, filter: TimesheetFilter) =>
    [...attendanceKeys.all, 'summary', audience, filter] as const,
  syncHealth: () => [...attendanceKeys.all, 'sync-health'] as const,
  syncSessions: (take: number) => [...attendanceKeys.all, 'sync-sessions', take] as const,
  unmappedCodes: () => [...attendanceKeys.all, 'unmapped-codes'] as const,
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

export const useSyncSessions = (take = 20) =>
  useQuery({
    queryKey: attendanceKeys.syncSessions(take),
    queryFn: () => attendanceApi.getSyncSessions(take),
  });

export const useUnmappedCodes = () =>
  useQuery({
    queryKey: attendanceKeys.unmappedCodes(),
    queryFn: attendanceApi.getUnmappedCodes,
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
