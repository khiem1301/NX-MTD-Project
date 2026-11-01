import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { API } from '@/constants/apiEndpoints';
import { MESSAGES } from '@/constants/messages';
import { useRolePath } from '@/hooks/useRolePath';
import { apiClient } from '@/services/apiClient';
import type { ApiError, ApiResponse } from '@/types/api';

export type ImportRow = {
  rowNumber: number;
  code: string | null;
  fullName: string | null;
  role: string | null;
  teamCode: string | null;
  status: 'VALID' | 'ERROR' | 'DUPLICATE';
  message: string | null;
};
export type ImportPreview = {
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  summary: string;
  rows: ImportRow[];
};
export type ImportCreated = { username: string; fullName: string; temporaryPassword: string };
export type ImportResult = {
  createdCount: number;
  failedCount: number;
  created: ImportCreated[];
  failed: ImportRow[];
};
const upload = async <T>(url: string, file: File): Promise<T> => {
  const form = new FormData();
  form.append('file', file);
  return (await apiClient.post<ApiResponse<T>>(url, form)).data.data;
};

/** Trạng thái và thao tác nhập nhân viên từ Excel: chọn file, xem trước, nhập, sao chép mật khẩu tạm. */
export function useEmployeeImport() {
  const queryClient = useQueryClient();
  const BASE = API.employees.import(useRolePath());
  const [file, setFile] = useState<File | null>(null);
  const preview = useMutation<ImportPreview, ApiError, File>({
    mutationFn: (f) => upload<ImportPreview>(`${BASE}/preview`, f),
    onError: (e) => void message.error(e.message),
  });
  const run = useMutation<ImportResult, ApiError, File>({
    mutationFn: (f) => upload<ImportResult>(BASE, f),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['employees'] }),
        queryClient.invalidateQueries({ queryKey: ['users'] }),
      ]);
    },
    onError: (e) => void message.error(e.message),
  });
  const data = preview.data;
  const result = run.data;

  const copyAll = async () => {
    if (!result) return;
    await navigator.clipboard.writeText(
      result.created.map((c) => `${c.username}\t${c.temporaryPassword}`).join('\n'),
    );
    void message.success(MESSAGES.hr.copied);
  };

  return { file, setFile, preview, run, data, result, copyAll };
}
