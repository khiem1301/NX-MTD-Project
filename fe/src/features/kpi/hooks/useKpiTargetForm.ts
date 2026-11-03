import { useEffect, useMemo } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { useOrderLineLookup, useOrderLookup } from '@/hooks/useOrderLookups';
import { fill, formatDate, toApiDate } from '@/utils/format';
import {
  useAllottedTeams,
  useKpiLineCapacity,
  useKpiOrderCapacity,
  useSaveKpiTarget,
} from './useKpi';
import type { Props } from '../components/KpiTargetFormModal';
import { type Dayjs } from 'dayjs';
import { KPI_TEXT } from '../constants';
import { dueWarning } from '../utils/dueWarning';
import {
  lineQtyError,
  sumLineQty,
  toLineQtyValues,
  toPayloadLines,
  type LineQtyValues,
} from '../utils/lineTargets';

const text = KPI_TEXT.form;

/** Trạng thái và xử lý của KpiTargetFormModal; component chỉ lo giao diện. */
export function useKpiTargetForm(props: Pick<Props, 'open' | 'target' | 'onClose'>) {
  const { open, target, onClose } = props;
  const [form] = Form.useForm<FormValues>();
  const save = useSaveKpiTarget(target?.kpiTargetId ?? null);
  const orderId = Form.useWatch('orderId', form) ?? null;
  const productId = Form.useWatch('productId', form) ?? null;
  const sewingTeamId = Form.useWatch('sewingTeamId', form) ?? null;
  const lineQty = Form.useWatch('lineQty', form);
  const capacity = useKpiOrderCapacity(orderId, productId, target?.kpiTargetId).data;
  const lineCapacity = useKpiLineCapacity(orderId, productId, sewingTeamId, target?.kpiTargetId);
  const lineRows = useMemo(() => lineCapacity.data ?? [], [lineCapacity.data]);
  // Chỉ tiêu cả kỳ = tổng các màu × size (DEV-ALL-62).
  const total = sumLineQty(lineRows, lineQty);
  const hasLineError = lineRows.some((l) => lineQtyError(l, lineQty?.[String(l.orderLineId)]));
  const overCapacity = capacity !== undefined && total > capacity.maxTarget;
  const orderClosed = capacity?.orderClosed === true;
  const canSave = total > 0 && !hasLineError && !overCapacity && !orderClosed;
  const period = Form.useWatch('period', form);
  const warning =
    capacity?.dueDate && period?.[1]
      ? dueWarning(toApiDate(period[1]), capacity.dueDate, capacity.dueWarningDays)
      : null;
  const orders = useOrderLookup();
  const lines = useOrderLineLookup(orderId);
  const teams = useAllottedTeams(orderId, productId);
  const editing = target !== null;
  /** Chỉ tiêu cũ chỉ có số tổng: phải nhập lại theo màu × size khi sửa. */
  const legacyTotal =
    target !== null && (target.lines ?? []).length === 0 ? target.targetFinishedQty : null;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (target) {
      form.setFieldsValue({
        orderId: target.orderId,
        productId: target.productId,
        sewingTeamId: target.sewingTeamId,
        period: [dayjs(target.startDate), dayjs(target.endDate)],
        lineQty: toLineQtyValues(target.lines),
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

  const options = useMemo(() => {
    const allotted = (teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }));
    return target && !allotted.some((o) => o.value === target.sewingTeamId)
      ? [{ value: target.sewingTeamId, label: target.sewingTeamName }, ...allotted]
      : allotted;
  }, [teams.data, target]);

  const teamSelectProps = {
    options,
    loading: teams.isFetching,
    notFoundContent: productId === null ? text.teamPickProduct : text.teamNotAllotted,
  };

  // Đổi đơn hoặc mã hàng mà tổ đang chọn không được chia mã mới thì bỏ chọn tổ.
  useEffect(() => {
    if (editing || !teams.data || sewingTeamId === null) return;
    if (!teams.data.some((t) => t.teamId === sewingTeamId))
      form.setFieldValue('sewingTeamId', undefined);
  }, [editing, teams.data, sewingTeamId, form]);

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
        lines: toPayloadLines(lineRows, values.lineQty),
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
    canSave,
    total,
    lineQty,
    lineRows,
    lineLoading: lineCapacity.isFetching,
    hasTeam: sewingTeamId !== null,
    legacyTotal,
    warning,
    disabledDate,
    orders,
    lines,
    editing,
    orderOptions,
    productOptions,
    teamSelectProps,
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
  lineQty?: LineQtyValues;
};
