import { useEffect, useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { fill, toApiDate } from '@/utils/format';
import {
  useImportOperations,
  useImportOperationsData,
  usePreviewOperationImport,
  usePreviewOperationImportData,
} from './useOperations';
import { confirmDialog } from '@/utils/confirmDialog';
import { PRODUCTION_TEXT } from '../constants';
import type {
  OperationImportParams,
  OperationImportPreview,
  OperationImportResult,
} from '../types';
import {
  blankDraftRow,
  draftFromPreview,
  sheetBodyOf,
  type OperationDraftRow,
} from '../utils/operationImportDraft';

const text = PRODUCTION_TEXT.operations.import;

/** Chờ người dùng ngừng gõ rồi mới kiểm lại (ms). */
const RECHECK_DELAY_MS = 500;

/**
 * Trạng thái và thao tác của màn nhập công đoạn từ Excel: chọn file, xem trước, sửa từng ô (DEV-ALL-179), tùy chọn đơn giá,
 * xác nhận nhập. Chưa sửa gì thì nhập thẳng từ file như trước; đã sửa thì kiểm lại và nhập từ bảng đã sửa.
 */
export function useOperationImport(productId: number, onClose: () => void) {
  const [file, setFile] = useState<File | null>(null);
  const [finalStt, setFinalStt] = useState<number | undefined>(undefined);
  const [withRates, setWithRates] = useState(true);
  const [effectiveDate, setEffectiveDate] = useState<Dayjs>(dayjs());
  const [submitRates, setSubmitRates] = useState(true);
  const [replace, setReplaceState] = useState(false);
  const preview = usePreviewOperationImport();
  const previewData = usePreviewOperationImportData();
  const importOps = useImportOperations();
  const importData = useImportOperationsData();
  const base = preview.data;

  // Bản nháp sửa được: bắt đầu từ kết quả xem trước file (đọc file / đổi sheet mới thì bắt đầu lại).
  const [rows, setRows] = useState<OperationDraftRow[]>([]);
  const [edited, setEdited] = useState(false);
  const [checked, setChecked] = useState<OperationImportPreview>();
  // Đã sửa mà chưa kiểm lại xong: chưa cho nhập (kết quả đang hiện là của bản sửa trước).
  const [stale, setStale] = useState(false);
  const [seenBase, setSeenBase] = useState(base);
  if (base !== seenBase) {
    // Kết quả xem trước mới (file mới, đổi sheet): bản nháp bắt đầu lại từ đó.
    setSeenBase(base);
    setRows(base ? draftFromPreview(base.rows) : []);
    setEdited(false);
    setChecked(undefined);
    setStale(false);
  }

  const data: OperationImportPreview | undefined = edited ? (checked ?? base) : base;
  const ratesOn = withRates && data?.hasRateColumn === true;

  const runCheck = (options: { withRates?: boolean; date?: Dayjs; replace?: boolean } = {}) => {
    if (!base) return;
    const useRates = options.withRates ?? withRates;
    const date = options.date ?? effectiveDate;
    previewData.mutate(
      {
        body: sheetBodyOf(base, rows),
        productId,
        effectiveDate: useRates ? toApiDate(date) : undefined,
        replace: (options.replace ?? replace) || undefined,
      },
      {
        onSuccess: (result) => {
          setChecked(result);
          setStale(false);
        },
      },
    );
  };

  // Sửa bản nháp thì máy chủ kiểm lại sau khi người dùng ngừng gõ.
  useEffect(() => {
    if (!edited) return;
    const timer = setTimeout(() => runCheck(), RECHECK_DELAY_MS);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, edited]);

  const change = (next: OperationDraftRow[]) => {
    setEdited(true);
    setStale(true);
    setRows(next);
  };

  const runPreview = (
    nextFile: File,
    options: { sheetName?: string; withRates?: boolean; date?: Dayjs; replace?: boolean } = {},
  ) => {
    setFinalStt(undefined);
    const useRates = options.withRates ?? withRates;
    const date = options.date ?? effectiveDate;
    // Chọn file mới truyền sheetName: undefined (đọc sheet đầu); đổi ngày / tùy chọn thì giữ sheet đang xem.
    const sheetName = 'sheetName' in options ? options.sheetName : base?.sheetName;
    preview.mutate(
      {
        file: nextFile,
        productId,
        sheetName,
        effectiveDate: useRates ? toApiDate(date) : undefined,
        replace: (options.replace ?? replace) || undefined,
      },
      {
        // DEV-ALL-116: chọn sẵn công đoạn cuối theo cột CUỐI (khi mã hàng chưa có công đoạn cuối).
        onSuccess: (result) =>
          setFinalStt(
            result.productHasFinalStep ? undefined : (result.finalSttInFile ?? undefined),
          ),
      },
    );
  };

  /** Đổi tùy chọn (đơn giá, ngày, thay thế): đã sửa bảng thì kiểm lại bản sửa, chưa sửa thì đọc lại file. */
  const reapply = (options: { withRates?: boolean; date?: Dayjs; replace?: boolean }) => {
    if (edited) runCheck(options);
    else if (file) runPreview(file, options);
  };

  const setReplace = (next: boolean) => {
    setReplaceState(next);
    reapply({ replace: next });
  };

  const showResult = (result: OperationImportResult) => {
    const rates = ratesOn
      ? fill(
          text.rateResultTemplate,
          result.rateCreated,
          result.rateUpdated,
          result.rateUnchanged,
          result.rateSkipped,
        )
      : '';
    void message.success(
      fill(text.resultTemplate, result.created, result.updated, result.unchanged, result.skipped) +
        rates +
        (result.removed > 0
          ? ` Đã ngừng dùng ${result.removed} công đoạn không có trong file.`
          : ''),
    );
    onClose();
  };

  const handleConfirm = () => {
    if (!file || !data || !base) return;
    const removed = replace ? (data.removeCount ?? 0) : 0;
    const params: OperationImportParams = {
      productId,
      sheetName: data.sheetName,
      finalStt,
      effectiveDate: ratesOn ? toApiDate(effectiveDate) : undefined,
      submitRates: ratesOn ? submitRates : undefined,
      replace: replace || undefined,
    };
    confirmDialog({
      title: text.confirmTitle,
      content:
        removed > 0
          ? `${text.confirmContent} ${fill(MESSAGES.common.importReplace.confirmExtra, removed)}`
          : text.confirmContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () =>
        (edited
          ? importData.mutateAsync({ body: sheetBodyOf(base, rows), params })
          : importOps.mutateAsync({ file, params })
        ).then(showResult),
    });
  };

  const rateChanges = ratesOn ? (data?.rateNewCount ?? 0) + (data?.rateUpdateCount ?? 0) : 0;
  const importable =
    (data?.newCount ?? 0) +
    (data?.updateCount ?? 0) +
    (replace ? (data?.removeCount ?? 0) : 0) +
    rateChanges +
    (finalStt !== undefined ? 1 : 0);
  const finalOptions = (data?.rows ?? [])
    .filter((r) => r.action !== 'Error')
    .map((r) => ({ value: r.stt, label: `${r.stt}. ${r.operationName ?? ''}` }));

  return {
    file,
    setFile,
    finalStt,
    setFinalStt,
    withRates,
    setWithRates,
    effectiveDate,
    setEffectiveDate,
    submitRates,
    setSubmitRates,
    replace,
    setReplace,
    preview,
    importOps: { isPending: importOps.isPending || importData.isPending },
    data,
    ratesOn,
    runPreview,
    reapply,
    handleConfirm,
    importable,
    finalOptions,
    // Sửa bảng (DEV-ALL-179)
    rows,
    checking: edited && (previewData.isPending || stale),
    checkError: previewData.error?.message,
    update: (rowNumber: number, patch: Partial<OperationDraftRow>) =>
      change(
        rows.map((r) => (r.rowNumber === rowNumber ? { ...r, ...patch, parseError: null } : r)),
      ),
    remove: (rowNumber: number) => change(rows.filter((r) => r.rowNumber !== rowNumber)),
    add: () => change([...rows, blankDraftRow(rows)]),
    reset: () => {
      if (base) {
        setRows(draftFromPreview(base.rows));
        setEdited(false);
        setChecked(undefined);
        setStale(false);
      }
    },
    edited,
  };
}
