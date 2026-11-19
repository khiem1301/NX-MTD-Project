import { useState } from 'react';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import type { SpecImportTarget } from '../types';
import { usePreviewSpecImport, useSpecImport } from './useSpecImport';

const text = MESSAGES.orders.specImport;

export type SpecImportProductOption = {
  productId: number;
  productCode: string;
  productName: string;
};

/**
 * Nơi mở hộp thoại: màn Mã hàng (Admin) biết mã hàng, chọn đơn nhận bìa tùy ý; màn Đơn hàng (Admin, Quản đốc) biết đơn,
 * chọn mã hàng của đơn (đơn một mã hàng thì chọn sẵn).
 */
export type SpecImportSource =
  | { scope: 'admin'; productId: number; productLabel: string }
  | { scope: 'manager'; orderId: number; orderCode: string; products: SpecImportProductOption[] };

const targetOf = (
  source: SpecImportSource,
  orderId: number | undefined,
  productId: number | undefined,
): SpecImportTarget | null => {
  if (source.scope === 'admin') return { scope: 'admin', productId: source.productId, orderId };
  return productId === undefined ? null : { scope: 'manager', orderId: source.orderId, productId };
};

/** DEV-ALL-115: trạng thái hộp thoại nhập Excel — chọn file, chọn đích, xem trước, xác nhận nhập (lưu ngay). */
export function useSpecImportModal(source: SpecImportSource, onClose: () => void) {
  const [file, setFile] = useState<File | null>(null);
  const [orderId, setOrderId] = useState<number | undefined>(undefined);
  const [productId, setProductId] = useState<number | undefined>(
    source.scope === 'manager' && source.products.length === 1
      ? source.products[0]?.productId
      : undefined,
  );
  const preview = usePreviewSpecImport();
  const doImport = useSpecImport();
  const target = targetOf(source, orderId, productId);

  const runPreview = (nextFile: File | null, nextTarget: SpecImportTarget | null) => {
    if (!nextFile || !nextTarget) return;
    preview.mutate({ target: nextTarget, file: nextFile });
  };

  const chooseFile = (nextFile: File | null) => {
    setFile(nextFile);
    if (nextFile) runPreview(nextFile, target);
    else preview.reset();
  };

  const chooseOrder = (next: number | undefined) => {
    setOrderId(next);
    runPreview(file, targetOf(source, next, productId));
  };

  const chooseProduct = (next: number | undefined) => {
    setProductId(next);
    preview.reset();
    runPreview(file, targetOf(source, orderId, next));
  };

  const handleConfirm = () => {
    if (!file || !target) return;
    confirmDialog({
      title: text.confirmTitle,
      content: text.confirmBody,
      onOk: () =>
        doImport.mutateAsync({ target, file }).then((result) => {
          void message.success(result.message ?? MESSAGES.common.saved);
          onClose();
        }),
    });
  };

  return {
    file,
    orderId,
    productId,
    preview,
    doImport,
    data: preview.data,
    canImport: preview.data?.canImport === true && !preview.isPending,
    chooseFile,
    chooseOrder,
    chooseProduct,
    handleConfirm,
  };
}
