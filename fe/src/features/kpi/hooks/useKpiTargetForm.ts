import { useEffect, useMemo } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { useOrderLineLookup, useOrderLookup } from '@/hooks/useOrderLookups';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { fill, formatDate, toApiDate } from '@/utils/format';
import { useKpiOrderCapacity, useSaveKpiTarget } from './useKpi';
import type { Props } from '../components/KpiTargetFormModal';
import { type Dayjs } from 'dayjs';
import { KPI_TEXT } from '../constants';
import { dueWarning } from '../utils/dueWarning';

const text = KPI_TEXT.form;

/** Trạng thái và xử lý của KpiTargetFormModal; component chỉ lo giao diện. */
export function useKpiTargetForm(props: Pick<Props, 'open' | 'target' | 'onClose'>) {
  const { open, target, onClose } = props;
  const [form] = Form.useForm<FormValues>();
  const save = useSaveKpiTarget(target?.kpiTargetId ?? null);
  const orderId = Form.useWatch('orderId', form) ?? null;
  const productId = Form.useWatch('productId', form) ?? null;
  const qty = Form.useWatch('targetFinishedQty', form);
  const capacity = useKpiOrderCapacity(orderId, productId, target?.kpiTargetId).data;
  const overCapacity = capacity !== undefined && (qty ?? 0) > capacity.maxTarget;
  const orderClosed = capacity?.orderClosed === true;
  const period = Form.useWatch('period', form);
  const warning =
    capacity?.dueDate && period?.[1]
      ? dueWarning(toApiDate(period[1]), capacity.dueDate, capacity.dueWarningDays)
      : null;
  const orders = useOrderLookup();
  const lines = useOrderLineLookup(orderId);
  const teams = useTeamLookup();
  const editing = target !== null;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (target) {
      form.setFieldsValue({
        orderId: target.orderId,
        productId: target.productId,
        sewingTeamId: target.sewingTeamId,
        period: [dayjs(target.startDate), dayjs(target.endDate)],
        targetFinishedQty: target.targetFinishedQty,
      });
    }
  }, [open, target, form]);

  const orderOptions = useMemo(() => {
    const options = (orders.data?.items ?? []).map((o) => ({
      value: o.orderId,
      label: fill(text.orderOption, o.orderCode, formatDate(o.dueDate)),
    }));
    // Đơn của chỉ tiêu đang sửa có thể đã ra khỏi danh sách "đơn đang chạy": vẫn phải hiện đúng mã.
    return target && !options.some((o) => o.value === target.orderId)
      ? [{ value: target.orderId, label: target.orderCode }, ...options]
      : options;
  }, [orders.data, target]);

  const productOptions = useMemo(() => {
    const seen = new Map<number, string>();
    (lines.data ?? []).forEach((l) => seen.set(l.productId, `${l.productCode} - ${l.productName}`));
    if (target && !seen.has(target.productId))
      seen.set(target.productId, `${target.productCode} - ${target.productName}`);
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [lines.data, target]);

  const teamOptions = (teams.data ?? [])
    .filter((t) => t.teamType === 'Sewing')
    .map((t) => ({ value: t.teamId, label: t.teamName }));

  const required = [{ required: true, message: text.required }];
  // Lập mới không chọn được ngày đã qua; chỉ tiêu đang chạy có thể đã bắt đầu từ trước nên để BE kiểm mốc bị đổi.
  const disabledDate = editing ? undefined : (date: Dayjs) => date.isBefore(dayjs(), 'day');

  const handleFinish = (values: FormValues) => {
    save.mutate(
      {
        orderId: values.orderId,
        productId: values.productId,
        sewingTeamId: values.sewingTeamId,
        startDate: toApiDate(values.period[0]),
        endDate: toApiDate(values.period[1]),
        targetFinishedQty: values.targetFinishedQty,
      },
      { onSuccess: onClose },
    );
  };

  return {
    save,
    orderId,
    productId,
    capacity,
    overCapacity,
    orderClosed,
    warning,
    disabledDate,
    orders,
    lines,
    teams,
    editing,
    orderOptions,
    productOptions,
    teamOptions,
    required,
    handleFinish,
    form,
  };
}

type FormValues = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  period: [Dayjs, Dayjs];
  targetFinishedQty: number;
};
