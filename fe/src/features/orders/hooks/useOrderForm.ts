import { Form, message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useBlocker, useNavigate } from 'react-router-dom';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ORDER_LIMITED_EDIT_STATUSES, ORDER_MATERIALS_TAB } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { applyFieldErrors } from '@/utils/apiError';
import { fill, toApiDate } from '@/utils/format';
import {
  hasImportFiles,
  runProductImports,
  showImportResults,
  showProductImportResults,
  type ProductImportSelection,
} from '../utils/productExcelImports';
import {
  hasOrderImports,
  runOrderImports,
  type OrderImportProduct,
  type OrderImportSelection,
} from '../utils/orderExcelImports';
import { useOrderExcelImports } from './useOrderExcelImports';
import { useCreateOrder, useCustomerOptions, useNextOrderCode, useUpdateOrder } from './useOrders';
import {
  blocksTotals,
  cloneBlocksForNewOrder,
  fromLines,
  newBlock,
  toLines,
  type ProductBlock,
} from '../utils/orderGrid';
import { validateBlocks } from '../utils/orderValidation';
import type { OrderFormProps } from '../components/OrderForm';
import type { OrderSaveResult, SaveProductPayload, StagedProductInput } from '../types';

export type HeaderValues = {
  orderCode: string;
  customerId: number;
  receivedDate: Dayjs;
  dueDate: Dayjs;
  deliveryAddress?: string;
  note?: string;
};

/** Trạng thái và xử lý của OrderForm; component chỉ lo giao diện. */
export function useOrderForm(props: Pick<OrderFormProps, 'orderId' | 'detail' | 'copyFrom'>) {
  const { orderId, detail, copyFrom } = props;
  const isEdit = orderId !== null;
  const navigate = useNavigate();
  const [form] = Form.useForm<HeaderValues>();
  const nextCode = useNextOrderCode(!isEdit);
  const customers = useCustomerOptions();
  const create = useCreateOrder();
  const update = useUpdateOrder(orderId ?? 0);
  const seedLines = detail?.lines ?? copyFrom?.lines;

  const [blocks, setBlocks] = useState<ProductBlock[]>(() =>
    detail
      ? fromLines(detail.lines)
      : copyFrom
        ? cloneBlocksForNewOrder(copyFrom.lines)
        : [newBlock()],
  );
  const [labels, setLabels] = useState<Record<number, string>>(() =>
    Object.fromEntries((seedLines ?? []).map((l) => [l.productId, `${l.productCode}`])),
  );
  const [lineError, setLineError] = useState<string>();
  const [showBlockErrors, setShowBlockErrors] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [customerOpen, setCustomerOpen] = useState(false);
  const [productBlock, setProductBlock] = useState<string>();
  // DEV-ALL-151: mã hàng khai trên form, chưa ghi. tempId âm. File Excel nhập sau khi Lưu đơn.
  const tempSeq = useRef(0);
  const [staged, setStaged] = useState<{ tempId: number; body: SaveProductPayload }[]>([]);
  const [pendingImports, setPendingImports] = useState<
    Record<number, { code: string; selection: ProductImportSelection }>
  >({});
  // DEV-ALL-150: Bìa của đơn, NVL của đơn đọc từ Excel; nhập vào đơn sau khi Lưu đơn.
  const orderImports = useOrderExcelImports(() => setDirty(true));
  const locked = isEdit && detail !== undefined && detail.status !== 'New';
  const editable =
    !isEdit ||
    (detail !== undefined &&
      (detail.status === 'New' || ORDER_LIMITED_EDIT_STATUSES.includes(detail.status)));

  useEffect(() => {
    if (!isEdit && nextCode.data) form.setFieldsValue({ orderCode: nextCode.data });
  }, [isEdit, nextCode.data, form]);

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty && currentLocation.pathname !== nextLocation.pathname,
  );
  // Hộp hỏi nằm ở OrderForm (UnsavedChangesModal), không dùng hộp thoại gốc của trình duyệt.
  const leaveGuard = {
    open: blocker.state === 'blocked',
    leave: () => blocker.proceed?.(),
    stay: () => blocker.reset?.(),
  };

  const totals = useMemo(() => blocksTotals(blocks), [blocks]);
  // Chỉ báo sau lần bấm Lưu đầu tiên; từ đó lỗi tự mất ngay khi người dùng sửa xong.
  const blockErrors = useMemo(
    () => (showBlockErrors ? validateBlocks(blocks, locked) : {}),
    [showBlockErrors, blocks, locked],
  );

  const changeBlocks = (next: ProductBlock[]) => {
    setBlocks(next);
    setDirty(true);
    setLineError(undefined);
    const used = new Set(next.map((b) => b.productId));
    setStaged((prev) => prev.filter((p) => used.has(p.tempId)));
    setPendingImports((prev) => {
      const kept: typeof prev = {};
      for (const [id, value] of Object.entries(prev)) {
        if (used.has(Number(id))) kept[Number(id)] = value;
      }
      return kept;
    });
  };

  /** Mã đã nằm trên đơn (khối khác) hoặc đang chờ ghi. exceptBlockKey: khối sắp nhận mã mới, không tính mã hiện tại của khối đó. */
  const codeInUse = (code: string, exceptBlockKey?: string) => {
    const upper = code.trim().toUpperCase();
    return (
      blocks.some(
        (b) =>
          b.key !== exceptBlockKey &&
          b.productId !== null &&
          labels[b.productId]?.toUpperCase() === upper,
      ) ||
      staged.some(
        (s) =>
          s.body.productCode.toUpperCase() === upper &&
          !blocks.some((b) => b.key === exceptBlockKey && b.productId === s.tempId),
      )
    );
  };

  /**
   * Giữ mã hàng trên form, không gọi API. Trả tempId âm; null khi mã đã có trên đơn.
   * Chưa gắn vào khối (nhập Excel gọi hàm này, Áp dụng mới gắn). Không đánh dấu form bẩn — gắn khối mới đánh dấu.
   */
  const stageProduct = (
    body: SaveProductPayload,
    selection: ProductImportSelection,
    exceptBlockKey?: string,
  ): number | null => {
    const productCode = body.productCode.trim().toUpperCase();
    if (codeInUse(productCode, exceptBlockKey)) {
      void message.error(fill(MESSAGES.orders.stagedDuplicate, productCode));
      return null;
    }
    const tempId = -(tempSeq.current += 1);
    const normalized = { ...body, productCode };
    setStaged((prev) => [...prev, { tempId, body: normalized }]);
    if (hasImportFiles(selection))
      setPendingImports((p) => ({ ...p, [tempId]: { code: productCode, selection } }));
    setLabels((l) => ({ ...l, [tempId]: productCode }));
    void message.success(fill(MESSAGES.orders.stagedWithOrder, productCode));
    return tempId;
  };

  /** Mã hàng đã khai trên form (chờ Lưu đơn), theo mã chữ hoa → tempId: hộp nhập dòng hàng nhận luôn, không hỏi tạo lại. */
  const stagedProducts: Record<string, number> = Object.fromEntries(
    staged.map((s) => [s.body.productCode, s.tempId]),
  );

  /** Gắn mã vừa khai vào khối đang mở form mã hàng. */
  const stageOntoBlock = (
    blockKey: string,
    body: SaveProductPayload,
    selection: ProductImportSelection,
  ) => {
    const tempId = stageProduct(body, selection, blockKey);
    if (tempId === null) return false;
    changeBlocks(blocks.map((b) => (b.key === blockKey ? { ...b, productId: tempId } : b)));
    return true;
  };

  /** Dữ liệu mã hàng đã khai trên form (chưa ghi) của một khối, để mở lại xem / sửa. */
  const stagedOfBlock = (blockKey: string) => {
    const block = blocks.find((b) => b.key === blockKey);
    const tempId = block?.productId;
    if (tempId === null || tempId === undefined || tempId >= 0) return undefined;
    const entry = staged.find((s) => s.tempId === tempId);
    return entry
      ? { tempId, body: entry.body, selection: pendingImports[tempId]?.selection }
      : undefined;
  };

  /** Lưu lại thông tin mã hàng đã khai trên form (cùng tempId): đổi mã, mô tả, ảnh, file Excel. Trả false khi mã đã có trên đơn. */
  const updateStagedProduct = (
    blockKey: string,
    body: SaveProductPayload,
    selection: ProductImportSelection,
  ): boolean => {
    const current = stagedOfBlock(blockKey);
    if (!current) return false;
    const productCode = body.productCode.trim().toUpperCase();
    if (codeInUse(productCode, blockKey)) {
      void message.error(fill(MESSAGES.orders.stagedDuplicate, productCode));
      return false;
    }
    const { tempId } = current;
    setStaged((prev) =>
      prev.map((s) => (s.tempId === tempId ? { tempId, body: { ...body, productCode } } : s)),
    );
    setPendingImports((prev) => {
      const { [tempId]: _old, ...rest } = prev;
      return hasImportFiles(selection)
        ? { ...rest, [tempId]: { code: productCode, selection } }
        : rest;
    });
    setLabels((l) => ({ ...l, [tempId]: productCode }));
    setDirty(true);
    void message.success(fill(MESSAGES.orders.stagedUpdated, productCode));
    return true;
  };

  /** Bỏ mã khai trong hộp nhập Excel khi đóng hộp mà không Áp dụng. */
  const unstageProduct = (tempId: number) => {
    setStaged((prev) => prev.filter((p) => p.tempId !== tempId));
    setPendingImports((prev) => {
      const { [tempId]: _removed, ...rest } = prev;
      return rest;
    });
    setLabels((prev) => {
      const { [tempId]: _removed, ...rest } = prev;
      return rest;
    });
  };

  const handleError = (error: ApiError) => {
    if (error.errorCode === ERROR_CODES.OrderCodeDuplicate)
      form.setFields([{ name: 'orderCode', errors: [error.message] }]);
    else if (LINE_ERROR_CODES.includes(error.errorCode)) setLineError(error.message);
    else if (applyFieldErrors(form, error)) {
      // Lỗi 400 của dòng hàng có khóa dạng lines[0].color, không gắn được vào ô nào của form.
      const lineErrors = Object.entries(error.errors ?? {})
        .filter(([field]) => !HEADER_FIELDS.includes(field))
        .flatMap(([, messages]) => messages);
      if (lineErrors.length > 0) setLineError([...new Set(lineErrors)].join(' '));
    }
  };

  const handleSave = () => {
    setShowBlockErrors(true);
    setLineError(undefined);
    form.submit();
  };

  const handleFinish = (values: HeaderValues) => {
    if (Object.keys(validateBlocks(blocks, locked)).length > 0) return;
    const lines = toLines(blocks);
    if (lines.length === 0) {
      setLineError(MESSAGES.orders.needLine);
      return;
    }
    const used = new Set(blocks.map((b) => b.productId));
    const stagedProducts: StagedProductInput[] = staged
      .filter((s) => used.has(s.tempId))
      .map((s) => ({ tempId: s.tempId, ...s.body }));
    const body = {
      orderCode: values.orderCode.trim(),
      customerId: values.customerId,
      receivedDate: toApiDate(values.receivedDate),
      dueDate: toApiDate(values.dueDate),
      deliveryAddress: values.deliveryAddress?.trim() ?? '',
      note: values.note?.trim() || null,
      lines,
      ...(stagedProducts.length > 0 ? { stagedProducts } : {}),
    };
    setDirty(false);
    // Mã hàng tạo trên form còn trong đơn và có file chờ: nhập định mức sau khi đơn đã tạo màu, size.
    const queued = Object.entries(pendingImports)
      .map(([id, v]) => ({ productId: Number(id), ...v }))
      .filter((q) => used.has(q.productId));
    const orderSelection = orderImports.selection;
    const productIds = [
      ...new Set(blocks.map((b) => b.productId).filter((id): id is number => id !== null)),
    ];
    const afterSave = (path: string, saved: OrderSaveResult) => {
      const realId = new Map(saved.products.map((p) => [p.tempId, p.productId]));
      const idOf = (id: number) => realId.get(id) ?? id;
      setPendingImports({});
      setStaged([]);
      orderImports.reset();
      navigate(path);
      const imports = queued.map((q) => ({ ...q, productId: idOf(q.productId) }));
      const products: OrderImportProduct[] = productIds.map((productId) => ({
        productId: idOf(productId),
        productCode: labels[productId] ?? '',
      }));
      if (imports.length > 0 || hasOrderImports(orderSelection))
        void runImportsAfterSave(
          imports,
          saved.orderId,
          values.orderCode.trim(),
          orderSelection,
          products,
        );
    };
    const fail = (e: ApiError) => {
      setDirty(true);
      handleError(e);
    };
    if (isEdit && detail) {
      update.mutate(
        { ...body, rowVer: detail.rowVer },
        {
          onSuccess: (saved) => afterSave(ROUTES.orderDetail(saved.orderId), saved),
          onError: fail,
        },
      );
    } else {
      create.mutate(body, {
        // Đơn tạo xong mới khai báo NVL: mở thẳng tab NVL của đơn.
        onSuccess: (saved) =>
          afterSave(`${ROUTES.orderDetail(saved.orderId)}?tab=${ORDER_MATERIALS_TAB}`, saved),
        onError: fail,
      });
    }
  };

  const initialValues = detail
    ? {
        orderCode: detail.orderCode,
        customerId: detail.customerId,
        receivedDate: dayjs(detail.receivedDate),
        dueDate: dayjs(detail.dueDate),
        deliveryAddress: detail.deliveryAddress,
        note: detail.note ?? undefined,
      }
    : copyFrom
      ? {
          customerId: copyFrom.customerId,
          receivedDate: dayjs(),
          deliveryAddress: copyFrom.deliveryAddress,
          note: copyFrom.note ?? undefined,
        }
      : { receivedDate: dayjs() };

  /** Tạo đơn: chọn khách thì điền sẵn địa chỉ mặc định của khách vào ô địa chỉ giao hàng, trừ khi người dùng đã tự gõ địa chỉ. */
  const fillAddressFromCustomer = (customerId: number) => {
    if (isEdit || form.isFieldTouched('deliveryAddress')) return;
    const address = customers.data?.find((c) => c.customerId === customerId)?.defaultAddress;
    form.setFieldValue('deliveryAddress', address ?? undefined);
  };

  return {
    initialValues,
    isEdit,
    navigate,
    customers,
    create,
    update,
    locked,
    editable,
    totals,
    changeBlocks,
    handleFinish,
    handleSave,
    leaveGuard,
    fillAddressFromCustomer,
    form,
    blocks,
    blockErrors,
    labels,
    setLabels,
    lineError,
    setDirty,
    customerOpen,
    setCustomerOpen,
    productBlock,
    setProductBlock,
    stageProduct,
    stagedProducts,
    /** Màu đã có định mức trong file Excel đã chọn của mã hàng khai trên form (tempId → màu). */
    stagedNormColors: Object.fromEntries(
      staged.map((s) => [
        s.tempId,
        [
          ...new Set(
            (pendingImports[s.tempId]?.selection.bundle?.spec.norms ?? []).map((n) => n.color),
          ),
        ],
      ]),
    ) as Record<number, string[]>,
    stageOntoBlock,
    stagedOfBlock,
    updateStagedProduct,
    unstageProduct,
    orderImports,
  };
}

type QueuedProductImport = { productId: number; code: string; selection: ProductImportSelection };

/**
 * Sau khi Lưu đơn: nhập định mức của mã hàng tạo nhanh trước (nhóm bìa cần có trước bìa của đơn), rồi Bìa của đơn,
 * NVL của đơn (DEV-ALL-150). Chạy nền, xong hiện kết quả.
 */
async function runImportsAfterSave(
  queued: QueuedProductImport[],
  orderId: number,
  orderCode: string,
  selection: OrderImportSelection,
  products: OrderImportProduct[],
) {
  for (const q of queued) {
    const hide = message.loading(fill(MESSAGES.orders.productExcel.runningOrder, q.code), 0);
    try {
      showProductImportResults(q.code, await runProductImports(q.productId, q.selection));
    } finally {
      hide();
    }
  }
  if (!hasOrderImports(selection)) return;
  const hide = message.loading(fill(MESSAGES.orders.orderExcel.running, orderCode), 0);
  try {
    const outcomes = await runOrderImports(orderId, selection, products);
    showImportResults(fill(MESSAGES.orders.orderExcel.saved, orderCode), outcomes);
  } finally {
    hide();
  }
}

const HEADER_FIELDS = [
  'orderCode',
  'customerId',
  'receivedDate',
  'dueDate',
  'deliveryAddress',
  'note',
];
const LINE_ERROR_CODES: string[] = [
  ERROR_CODES.OrderLineRemoveNotAllowed,
  ERROR_CODES.OrderLineKeyLocked,
  ERROR_CODES.ProductNotActive,
  ERROR_CODES.CommonDuplicate,
];
