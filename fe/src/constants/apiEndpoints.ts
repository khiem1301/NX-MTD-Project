export const API = {
  auth: {
    login: '/api/v1/common/auth/login',
    refresh: '/api/v1/common/auth/refresh',
    logout: '/api/v1/common/auth/logout',
    me: '/api/v1/common/auth/me',
  },
  attendance: {
    syncHealth: '/api/v1/accountant/attendance/sync-health',
    syncSessions: '/api/v1/accountant/attendance/sync-sessions',
    unmappedCodes: '/api/v1/accountant/attendance/unmapped-codes',
    mapCode: '/api/v1/accountant/attendance/map-code',
    timesheets: '/api/v1/accountant/attendance/timesheets',
    timesheetSummary: '/api/v1/accountant/attendance/timesheets/summary',
  },
  teamAttendance: {
    timesheets: '/api/v1/team-leader/attendance/timesheets',
    timesheetSummary: '/api/v1/team-leader/attendance/timesheets/summary',
  },
  myAttendance: {
    timesheets: '/api/v1/common/my-attendance/timesheets',
    timesheetSummary: '/api/v1/common/my-attendance/timesheets/summary',
  },
} as const;
