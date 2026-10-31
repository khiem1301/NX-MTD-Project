import { Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate } from 'react-router-dom';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { ORDER_LIMITED_EDIT_STATUSES } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { toApiDate } from '@/utils/format';
import { useCreateOrder, useCustomers, useNextOrderCode, useUpdateOrder } from './useOrders';
import { blocksTotals, fromLines, newBlock, toLines, type ProductBlock } from '../utils/orderGrid';
import type { OrderFormProps } from '../components/OrderForm';

export type HeaderValues = {
  orderCode: string;
  customerId: number;
  receivedDate: Dayjs;
  dueDate: Dayjs;
  note?: string;
};

/** Trạng thái và xử lý của OrderForm; component chỉ lo giao diện. */
export function useOrderForm(props: Pick<OrderFormProps, 'orderId' | 'detail'>) {
  const { orderId, detail } = props;
  const isEdit = orderId !== null;
  const navigate = useNavigate();
  const [form] = Form.useForm<HeaderValues>();
  const nextCode = useNextOrderCode(!isEdit);
  const customers = useCustomers();
  const create = useCreateOrder();
  const update = useUpdateOrder(orderId ?? 0);

  const [blocks, setBlocks] = useState<ProductBlock[]>(() =>
    detail ? fromLines(detail.lines) : [newBlock()],
  );
  const [labels, setLabels] = useState<Record<number, string>>(() =>
    Object.fromEntries(
      (detail?.lines ?? []).map((l) => [l.productId, `${l.productCode} · ${l.productName}`]),
    ),
  );
  const [lineError, setLineError] = useState<string>();
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
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    if (window.confirm(MESSAGES.orders.unsavedChanges)) blocker.proceed();
    else blocker.reset();
  }, [blocker]);

  const totals = useMemo(() => blocksTotals(blocks), [blocks]);

  const changeBlocks = (next: ProductBlock[]) => {
    setBlocks(next);
    setDirty(true);
    setLineError(undefined);
  };

  const handleError = (error: ApiError) => {
    if (error.errorCode === ERROR_CODES.OrderCodeDuplicate)
      form.setFields([{ name: 'orderCode', errors: [error.message] }]);
    else if (LINE_ERROR_CODES.includes(error.errorCode)) setLineError(error.message);
  };

  const handleFinish = (values: HeaderValues) => {
    const lines = toLines(blocks);
    if (lines.length === 0) {
      setLineError(MESSAGES.orders.needLine);
      return;
    }
    const chosen = blocks.map((b) => b.productId).filter((id): id is number => id !== null);
    if (new Set(chosen).size !== chosen.length) {
      setLineError(MESSAGES.orders.duplicateProduct);
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
        onSuccess: (newId) => done(newId as number),
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
    form,
    blocks,
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

const LINE_ERROR_CODES: string[] = [
  ERROR_CODES.OrderLineRemoveNotAllowed,
  ERROR_CODES.OrderLineKeyLocked,
  ERROR_CODES.ProductNotActive,
  ERROR_CODES.CommonDuplicate,
];
