import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { confirmDialog } from '@/utils/confirmDialog';
import { monthlyCostApi } from '../api/monthlyCostApi';
import type {
  MonthlyCostDraftRow,
  MonthlyCostExcelPreview,
  MonthlyCostImportResult,
} from '../types';
import { blankDraftRow, draftFromPreview, payloadOf } from '../utils/monthlyCostDraft';
import { usePreviewMonthlyCostImport } from './useMonthlyCost';

/** Chờ người dùng ngừng gõ rồi mới kiểm lại (ms). */
const RECHECK_DELAY_MS = 500;

/**
 * DEV-ALL-187: trạng thái popup nhập chi phí tháng từ Excel, cùng kiểu nhập đơn / mã hàng: chọn file là xem trước ngay, sửa từng ô
 * (máy chủ kiểm lại sau mỗi lần sửa), thêm / xóa dòng, rồi nhập từ bảng đã sửa. Chỉ gắn khi popup mở nên state tự reset.
 */
export function useMonthlyCostImport(periodMonth: string, onClose: () => void) {
  const queryClient = useQueryClient();
  const preview = usePreviewMonthlyCostImport(periodMonth);
  const base = preview.data;

  const [rows, setRows] = useState<MonthlyCostDraftRow[]>([]);
  const [edited, setEdited] = useState(false);
  const [checked, setChecked] = useState<MonthlyCostExcelPreview>();
  // Đã sửa mà chưa kiểm lại xong: chưa cho nhập (kết quả đang hiện là của bản sửa trước).
  const [stale, setStale] = useState(false);
  const [seenBase, setSeenBase] = useState(base);
  if (base !== seenBase) {
    // Kết quả xem trước mới (chọn file mới): bản nháp bắt đầu lại từ đó.
    setSeenBase(base);
    setRows(base ? draftFromPreview(base.rows) : []);
    setEdited(false);
    setChecked(undefined);
    setStale(false);
  }
  const data = edited ? (checked ?? base) : base;

  const recheck = useMutation<MonthlyCostExcelPreview, ApiError, MonthlyCostDraftRow[]>({
    mutationFn: (next) => monthlyCostApi.previewRows(payloadOf(periodMonth, next)),
  });
  const importRows = useMutation<MonthlyCostImportResult, ApiError, MonthlyCostDraftRow[]>({
    mutationFn: (next) => monthlyCostApi.importRows(payloadOf(periodMonth, next)),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['monthly-cost'] });
      void message.success(
        `${MESSAGES.monthlyCost.importedPrefix} ${result.itemCount} khoản, tổng ${result.totalAmount.toLocaleString('vi-VN')} đ.`,
      );
      onClose();
    },
    onError: (error) => void message.error(error.message),
  });

  // Sửa bản nháp thì máy chủ kiểm lại sau khi người dùng ngừng gõ.
  useEffect(() => {
    if (!edited) return;
    const timer = setTimeout(
      () =>
        recheck.mutate(rows, {
          onSuccess: (result) => {
            setChecked(result);
            setStale(false);
          },
        }),
      RECHECK_DELAY_MS,
    );
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, edited]);

  const change = (next: MonthlyCostDraftRow[]) => {
    setEdited(true);
    setStale(true);
    setRows(next);
  };

  const handleConfirm = () =>
    confirmDialog({
      title: MESSAGES.monthlyCost.confirmImportTitle,
      content: MESSAGES.monthlyCost.confirmImportContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () => importRows.mutateAsync(rows).then(() => undefined),
    });

  return {
    preview,
    data,
    rows,
    edited,
    checking: edited && (recheck.isPending || stale),
    checkError: recheck.error?.message,
    importing: importRows.isPending,
    importable: data?.validCount ?? 0,
    update: (rowNumber: number, patch: Partial<MonthlyCostDraftRow>) =>
      change(
        rows.map((r) => (r.rowNumber === rowNumber ? { ...r, ...patch, parseError: null } : r)),
      ),
    remove: (rowNumber: number) => change(rows.filter((r) => r.rowNumber !== rowNumber)),
    add: () => change([...rows, blankDraftRow(rows)]),
    reset: () => {
      if (!base) return;
      setRows(draftFromPreview(base.rows));
      setEdited(false);
      setChecked(undefined);
      setStale(false);
    },
    handleConfirm,
  };
}
