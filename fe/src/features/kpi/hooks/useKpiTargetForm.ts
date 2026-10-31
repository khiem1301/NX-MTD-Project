import { useEffect, useMemo } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { useOrderLineLookup, useOrderLookup } from '@/hooks/useOrderLookups';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { toApiDate } from '@/utils/format';
import { useKpiCapacity, useSaveKpiTarget } from './useKpi';
import type { Props } from '../components/KpiTargetFormModal';
import { type Dayjs } from 'dayjs';
import { KPI_TEXT } from '../constants';

const text = KPI_TEXT.form;

/** Trạng thái và xử lý của KpiTargetFormModal; component chỉ lo giao diện. */
export function useKpiTargetForm(props: Pick<Props, 'open' | 'target' | 'onClose'>) {
  const { open, target, onClose } = props;
  const [form] = Form.useForm<FormValues>();
  const save = useSaveKpiTarget(target?.kpiTargetId ?? null);
  const orderId = Form.useWatch('orderId', form) ?? null;
  const productId = Form.useWatch('productId', form) ?? null;
  const sewingTeamId = Form.useWatch('sewingTeamId', form) ?? null;
  const qty = Form.useWatch('targetFinishedQty', form);
  const capacity = useKpiCapacity(orderId, productId, sewingTeamId, target?.kpiTargetId).data;
  const overCapacity = capacity !== undefined && (qty ?? 0) > capacity.maxTarget;
  const days = Form.useWatch('workingDays', form);
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
        workingDays: target.workingDays,
      });
    }
  }, [open, target, form]);

  const orderOptions = useMemo(() => {
    const options = (orders.data?.items ?? []).map((o) => ({
      value: o.orderId,
      label: o.orderCode,
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

  const dailyPreview = qty && days ? Math.round((qty / days) * 100) / 100 : null;
  const required = [{ required: true, message: text.required }];

  const handleFinish = (values: FormValues) => {
    save.mutate(
      {
        orderId: values.orderId,
        productId: values.productId,
        sewingTeamId: values.sewingTeamId,
        startDate: toApiDate(values.period[0]),
        endDate: toApiDate(values.period[1]),
        targetFinishedQty: values.targetFinishedQty,
        workingDays: values.workingDays,
      },
      { onSuccess: onClose },
    );
  };

  return {
    save,
    orderId,
    productId,
    sewingTeamId,
    capacity,
    overCapacity,
    orders,
    lines,
    teams,
    editing,
    orderOptions,
    productOptions,
    teamOptions,
    dailyPreview,
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
  workingDays: number;
};
