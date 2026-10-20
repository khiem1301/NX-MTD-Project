import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App } from 'antd';
import type { ApiError } from '@/types/api';
import { attendanceApi, type TimesheetAudience } from '../api/attendanceApi';
import { ATTENDANCE_TEXT } from '../constants';
import type { MapAttendanceCodeRequest, TimesheetFilter } from '../types';

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
