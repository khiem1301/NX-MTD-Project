import { useMutation, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { accountingReportsApi } from '../api/accountingReportsApi';
import type { AccountingReportFilter } from '../types';

export const ACCOUNTING_REPORTS_KEY = ['accounting-reports'] as const;
const sectionKey = (section: string, filter: AccountingReportFilter) =>
  [...ACCOUNTING_REPORTS_KEY, section, filter] as const;

export const useWorkdayOvertimeReport = (filter: AccountingReportFilter) =>
  useQuery({
    queryKey: sectionKey('workdays', filter),
    queryFn: () => accountingReportsApi.getWorkdays(filter),
  });

export const useMealReport = (filter: AccountingReportFilter) =>
  useQuery({
    queryKey: sectionKey('meals', filter),
    queryFn: () => accountingReportsApi.getMeals(filter),
  });

export const useVehicleReport = (filter: AccountingReportFilter) =>
  useQuery({
    queryKey: sectionKey('vehicles', filter),
    queryFn: () => accountingReportsApi.getVehicles(filter),
  });

export const useExportAccountingReports = () =>
  useMutation<void, ApiError, AccountingReportFilter>({
    mutationFn: (filter) => accountingReportsApi.export(filter),
    onSuccess: () => void message.success(MESSAGES.accountingReports.exportSuccess),
    onError: () => void message.error(MESSAGES.accountingReports.exportFailed),
  });
