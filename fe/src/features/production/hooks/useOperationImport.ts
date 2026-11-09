import { useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { fill, toApiDate } from '@/utils/format';
import { useImportOperations, usePreviewOperationImport } from './useOperations';
import { confirmDialog } from '@/utils/confirmDialog';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.operations.import;

/** Trạng thái và thao tác của màn nhập công đoạn từ Excel: chọn file, xem trước, tùy chọn đơn giá, xác nhận nhập. */
export function useOperationImport(productId: number, onClose: () => void, allowRates: boolean) {
  const [file, setFile] = useState<File | null>(null);
  const [finalStt, setFinalStt] = useState<number | undefined>(undefined);
  const [withRates, setWithRates] = useState(allowRates);
  const [effectiveDate, setEffectiveDate] = useState<Dayjs>(dayjs());
  const [submitRates, setSubmitRates] = useState(true);
  const preview = usePreviewOperationImport();
  const importOps = useImportOperations();
  const data = preview.data;
  const ratesOn = withRates && data?.hasRateColumn === true;

  const runPreview = (
    nextFile: File,
    options: { sheetName?: string; withRates?: boolean; date?: Dayjs } = {},
  ) => {
    setFinalStt(undefined);
    const useRates = options.withRates ?? withRates;
    const date = options.date ?? effectiveDate;
    // Chọn file mới truyền sheetName: undefined (đọc sheet đầu); đổi ngày / tùy chọn thì giữ sheet đang xem.
    const sheetName = 'sheetName' in options ? options.sheetName : data?.sheetName;
    preview.mutate({
      file: nextFile,
      productId,
      sheetName,
      effectiveDate: useRates ? toApiDate(date) : undefined,
    });
  };

  const handleConfirm = () => {
    if (!file || !data) return;
    confirmDialog({
      title: text.confirmTitle,
      content: text.confirmContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: () =>
        importOps
          .mutateAsync({
            file,
            params: {
              productId,
              sheetName: data.sheetName,
              finalStt,
              effectiveDate: ratesOn ? toApiDate(effectiveDate) : undefined,
              submitRates: ratesOn ? submitRates : undefined,
            },
          })
          .then((result) => {
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
              fill(
                text.resultTemplate,
                result.created,
                result.updated,
                result.unchanged,
                result.skipped,
              ) + rates,
            );
            onClose();
          }),
    });
  };

  const rateChanges = ratesOn ? (data?.rateNewCount ?? 0) + (data?.rateUpdateCount ?? 0) : 0;
  const importable =
    (data?.newCount ?? 0) +
    (data?.updateCount ?? 0) +
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
    preview,
    importOps,
    data,
    ratesOn,
    runPreview,
    handleConfirm,
    importable,
    finalOptions,
  };
}
