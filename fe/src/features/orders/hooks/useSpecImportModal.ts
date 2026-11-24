import { useState } from 'react';
import { message } from 'antd';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { MESSAGES } from '@/constants/messages';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi, type ReviewScope } from '../api/orderApi';
import type { ExcelBundle, SpecImportPart } from '../types';
import { restrictBundle, type ReviewTable } from '../utils/excelReview';
import { specImportDoneMessage } from '../utils/importMessages';
import {
  createMaterials,
  refreshAfterImport,
  showImportResults,
} from '../utils/productExcelImports';

const text = MESSAGES.orders.specImport;

export type SpecImportProductOption = {
  productId: number;
  productCode: string;
};

/**
 * Nơi mở hộp thoại: màn Mã hàng (Admin) biết mã hàng, chọn đơn nhận bìa tùy ý; màn Đơn hàng (Admin, Quản đốc) biết đơn,
 * chọn mã hàng của đơn (đơn một mã hàng thì chọn sẵn).
 */
export type SpecImportSource =
  | { scope: 'admin'; productId: number; productLabel: string }
  | { scope: 'manager'; orderId: number; orderCode: string; products: SpecImportProductOption[] };

/** Popup đang mở: file đã đọc, người dùng sửa trực tiếp trước khi nhập. */
type Reviewing = { bundle: ExcelBundle; fileName: string };

/**
 * Bảng lấy từ file. Tab Bìa cắt của đơn (màn Đơn hàng) chỉ lấy bìa cắt, không đụng định mức mã hàng; màn Mã hàng lấy định mức, và
 * bìa của đơn khi đã chọn đơn.
 */
const reviewTablesOf = (scope: SpecImportSource['scope'], orderId: number | undefined): ReviewTable[] => {
  if (scope === 'manager') return ['markers'];
  return orderId === undefined ? ['components', 'norms', 'groups'] : ['components', 'norms', 'groups', 'markers'];
};

/** Bảng nào có trong file (và nằm trong phạm vi) thì mới gửi nhập. */
const partsOf = (
  bundle: ExcelBundle,
  orderId: number | undefined,
  tables: readonly ReviewTable[],
): SpecImportPart[] => {
  const spec = bundle.spec;
  const has: [SpecImportPart, boolean][] = [
    ['components', spec.hasComponents],
    ['norms', spec.hasNorms],
    ['groups', spec.hasMarkerGroups],
    ['markers', orderId !== undefined && spec.hasOrderMarkers],
  ];
  const inScope: Record<SpecImportPart, ReviewTable> = {
    components: 'components',
    norms: 'norms',
    groups: 'groups',
    markers: 'markers',
  };
  return has.filter(([part, present]) => present && tables.includes(inScope[part])).map(([part]) => part);
};

/**
 * DEV-ALL-115/175: trạng thái hộp thoại nhập Excel định mức + bìa của đơn — chọn đích, chọn file; đọc file xong thì mở popup kiểm tra
 * cho sửa từng ô (máy chủ kiểm lại sau mỗi lần sửa), Xác nhận thì nhập bảng đã sửa một lần.
 */
export function useSpecImportModal(source: SpecImportSource, onClose: () => void) {
  // API theo người đăng nhập: Admin dùng API Admin (cả ở màn Bìa cắt của đơn), Quản đốc dùng API Quản đốc.
  const scope: ReviewScope = useRolePath() === 'admin' ? 'admin' : 'manager';
  const [orderId, setOrderId] = useState<number | undefined>(undefined);
  const [productId, setProductId] = useState<number | undefined>(
    source.scope === 'manager' && source.products.length === 1
      ? source.products[0]?.productId
      : source.scope === 'admin'
        ? source.productId
        : undefined,
  );
  const [reviewing, setReviewing] = useState<Reviewing | null>(null);
  const effectiveOrderId = source.scope === 'manager' ? source.orderId : orderId;

  const parse = useApiMutation((file: File) => excelBundleApi.parse(file, scope), {
    invalidate: [],
  });
  const doImport = useApiMutation(
    (args: { bundle: ExcelBundle; replace: boolean }) =>
      excelBundleApi.importSpecScoped(
        scope,
        productId ?? 0,
        args.bundle.spec,
        effectiveOrderId,
        partsOf(args.bundle, effectiveOrderId, reviewTablesOf(source.scope, effectiveOrderId)),
        args.replace,
      ),
    { invalidate: [] },
  );

  const chooseFile = (file: File) => {
    if (productId === undefined) {
      void message.warning(text.productPlaceholder);
      return;
    }
    parse.mutate(file, { onSuccess: (bundle) => setReviewing({ bundle, fileName: file.name }) });
  };

  const confirm = async (
    edited: ExcelBundle,
    materials: SaveMaterialPayload[],
    replace: boolean,
  ) => {
    if (doImport.isPending) return;
    // Chỉ nhập đúng các bảng của hộp thoại này: bảng khác trong file không bị kiểm, không bị ghi.
    const bundle = restrictBundle(edited, reviewTablesOf(source.scope, effectiveOrderId));
    if (partsOf(bundle, effectiveOrderId, reviewTablesOf(source.scope, effectiveOrderId)).length === 0) {
      void message.warning(text.nothingToImport);
      return;
    }
    if (materials.length > 0) {
      const created = await createMaterials(materials);
      if (!created.ok) {
        showImportResults(text.confirmTitle, [created]);
        return;
      }
    }
    const parts = partsOf(bundle, effectiveOrderId, reviewTablesOf(source.scope, effectiveOrderId));
    doImport.mutate(
      { bundle, replace },
      {
        onSuccess: async (result) => {
          await refreshAfterImport();
          void message.success(specImportDoneMessage(result.data, parts, materials.length));
          setReviewing(null);
          onClose();
        },
      },
    );
  };

  return {
    scope,
    // Màn đơn hàng: đơn cố định theo màn; màn Mã hàng: đơn do người dùng chọn (có thể chưa chọn).
    orderId: effectiveOrderId,
    productId,
    reviewing,
    parsing: parse.isPending,
    parseError: parse.error?.message,
    importing: doImport.isPending,
    reviewTables: reviewTablesOf(source.scope, effectiveOrderId),
    chooseFile,
    chooseOrder: setOrderId,
    chooseProduct: setProductId,
    confirm,
    closeReview: () => setReviewing(null),
  };
}
