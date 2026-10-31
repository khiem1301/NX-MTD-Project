import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  AdjustmentFilter,
  AttendanceAdjustment,
  CreateAdjustmentPayload,
  AttendanceExcelPreview,
  AttendanceImportResult,
  DailyTimesheet,
  MapAttendanceCodeRequest,
  SyncHealth,
  SyncSession,
  TimesheetFilter,
  TimesheetSummary,
  UnmappedCode,
} from '../types';

const asFormData = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

/** Bỏ các khóa rỗng để URL không mang ?teamId=&keyword= vô nghĩa. */
const toParams = (filter: TimesheetFilter): Record<string, string | number | boolean> => {
  const params: Record<string, string | number | boolean> = {
    fromDate: filter.fromDate,
    toDate: filter.toDate,
    page: filter.page,
    pageSize: filter.pageSize,
  };
  if (filter.teamId !== undefined) params.teamId = filter.teamId;
  if (filter.employeeId !== undefined) params.employeeId = filter.employeeId;
  if (filter.onlyMissingPunch) params.onlyMissingPunch = true;
  if (filter.keyword) params.keyword = filter.keyword;
  return params;
};

/** Ba vai trò dùng chung màn hình bảng công, chỉ khác endpoint do phạm vi xem (GBR-01). */
export type TimesheetEndpoints = { timesheets: string; timesheetSummary: string };

export const TIMESHEET_ENDPOINTS = {
  accountant: API.attendance,
  teamLeader: API.teamAttendance,
  me: API.myAttendance,
} as const satisfies Record<string, TimesheetEndpoints>;

export type TimesheetAudience = keyof typeof TIMESHEET_ENDPOINTS;

export const attendanceApi = {
  getTimesheets: async (
    audience: TimesheetAudience,
    filter: TimesheetFilter,
  ): Promise<Paginated<DailyTimesheet>> => {
    const res = await apiClient.get<ApiResponse<Paginated<DailyTimesheet>>>(
      TIMESHEET_ENDPOINTS[audience].timesheets,
      { params: toParams(filter) },
    );
    return res.data.data;
  },

  getSummary: async (
    audience: TimesheetAudience,
    filter: TimesheetFilter,
  ): Promise<TimesheetSummary> => {
    const res = await apiClient.get<ApiResponse<TimesheetSummary>>(
      TIMESHEET_ENDPOINTS[audience].timesheetSummary,
      { params: toParams(filter) },
    );
    return res.data.data;
  },

  getSyncHealth: async (): Promise<SyncHealth> => {
    const res = await apiClient.get<ApiResponse<SyncHealth>>(API.attendance.syncHealth);
    return res.data.data;
  },

  getSyncSessions: async (take: number): Promise<SyncSession[]> => {
    const res = await apiClient.get<ApiResponse<SyncSession[]>>(API.attendance.syncSessions, {
      params: { take },
    });
    return res.data.data;
  },

  getUnmappedCodes: async (): Promise<UnmappedCode[]> => {
    const res = await apiClient.get<ApiResponse<UnmappedCode[]>>(API.attendance.unmappedCodes);
    return res.data.data;
  },

  mapCode: async (body: MapAttendanceCodeRequest): Promise<void> => {
    await apiClient.post(API.attendance.mapCode, body);
  },

  // ---------- Chấm công hộ (GBR-25) ----------

  getAdjustments: async (filter: AdjustmentFilter): Promise<Paginated<AttendanceAdjustment>> => {
    const res = await apiClient.get<ApiResponse<Paginated<AttendanceAdjustment>>>(
      API.attendance.adjustments,
      {
        params: { ...filter, keyword: filter.keyword || undefined },
      },
    );
    return res.data.data;
  },

  createAdjustment: async (body: CreateAdjustmentPayload): Promise<void> => {
    await apiClient.post(API.attendance.adjustments, body);
  },

  // ---------- Import Excel dự phòng (UC29) ----------

  previewExcelImport: async (file: File): Promise<AttendanceExcelPreview> => {
    const res = await apiClient.post<ApiResponse<AttendanceExcelPreview>>(
      API.attendance.importPreview,
      asFormData(file),
    );
    return res.data.data;
  },

  confirmExcelImport: async (file: File): Promise<AttendanceImportResult> => {
    const res = await apiClient.post<ApiResponse<AttendanceImportResult>>(
      API.attendance.import,
      asFormData(file),
    );
    return res.data.data;
  },
};
