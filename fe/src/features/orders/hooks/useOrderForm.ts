import { Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate } from 'react-router-dom';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ORDER_LIMITED_EDIT_STATUSES, ORDER_MATERIALS_TAB } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { applyFieldErrors } from '@/utils/apiError';
import { toApiDate } from '@/utils/format';
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

export type HeaderValues = {
  orderCode: string;
  customerId: number;
  receivedDate: Dayjs;
  dueDate: Dayjs;
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
    Object.fromEntries(
      (seedLines ?? []).map((l) => [l.productId, `${l.productCode} · ${l.productName}`]),
    ),
  );
  const [lineError, setLineError] = useState<string>();
  const [showBlockErrors, setShowBlockErrors] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [customerOpen, setCustomerOpen] = useState(false);
  const [productBlock, setProductBlock] = useState<string>();
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
    const body = {
      orderCode: values.orderCode.trim(),
      customerId: values.customerId,
      receivedDate: toApiDate(values.receivedDate),
      dueDate: toApiDate(values.dueDate),
      note: values.note?.trim() || null,
      lines,
    };
    setDirty(false);
    const done = (savedId: number) => navigate(ROUTES.orderDetail(savedId));
    if (isEdit && detail) {
      update.mutate(
        { ...body, rowVer: detail.rowVer },
        {
          onSuccess: () => done(detail.orderId),
          onError: (e) => {
            setDirty(true);
            handleError(e);
          },
        },
      );
    } else {
      create.mutate(body, {
        // Đơn tạo xong mới khai báo NVL: mở thẳng tab NVL của đơn.
        onSuccess: (newId) =>
          navigate(`${ROUTES.orderDetail(newId as number)}?tab=${ORDER_MATERIALS_TAB}`),
        onError: (e) => {
          setDirty(true);
          handleError(e);
        },
      });
    }
  };

  const initialValues = detail
    ? {
        orderCode: detail.orderCode,
        customerId: detail.customerId,
        receivedDate: dayjs(detail.receivedDate),
        dueDate: dayjs(detail.dueDate),
        note: detail.note ?? undefined,
      }
    : copyFrom
      ? {
          customerId: copyFrom.customerId,
          receivedDate: dayjs(),
          note: copyFrom.note ?? undefined,
        }
      : { receivedDate: dayjs() };

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
  };
}

const HEADER_FIELDS = ['orderCode', 'customerId', 'receivedDate', 'dueDate', 'note'];
const LINE_ERROR_CODES: string[] = [
  ERROR_CODES.OrderLineRemoveNotAllowed,
  ERROR_CODES.OrderLineKeyLocked,
  ERROR_CODES.ProductNotActive,
  ERROR_CODES.CommonDuplicate,
];
