import { useEffect, useMemo } from 'react';
import { DatePicker, Form, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineLookup, useOrderLookup } from '@/hooks/useOrderLookups';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { toApiDate } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useKpiTargetDetail, useSaveKpiTarget } from '../hooks/useKpi';
import type { KpiTarget } from '../types';
import { evenSplit, listWorkDays, rowsForPeriod, type DailyRow } from '../utils/dailyPlan';
import { KpiDailyPlanTable } from './KpiDailyPlanTable';

type Props = { open: boolean; target: KpiTarget | null; onClose: () => void };

type FormValues = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  period: [Dayjs, Dayjs];
  days: DailyRow[];
};

const DATE_FORMAT = 'DD/MM/YYYY';
const text = KPI_TEXT.form;

/**
 * UC19 — Quản đốc lập / sửa chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ. Mã hàng chọn trong số mã hàng
 * của đơn đã chọn; BE kiểm lại và báo trùng kỳ. Khi sửa, đơn / mã hàng / tổ không đổi được để khỏi lệch kết quả đã tính.
 * Bắt buộc nhập chỉ tiêu từng ngày (mấy ngày đầu vào đơn tổ làm chậm hơn); tổng và số ngày do BE tính từ các ngày.
 */
export function KpiTargetFormModal({ open, target, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveKpiTarget(target?.kpiTargetId ?? null);
  const orderId = Form.useWatch('orderId', form) ?? null;
  const period = Form.useWatch('period', form);
  const detail = useKpiTargetDetail(open && target ? target.kpiTargetId : null);
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
      });
    }
  }, [open, target, form]);

  // Chỉ tiêu từng ngày lấy từ chi tiết. KPI cũ chưa có ngày: dựng các ngày trong kỳ (bỏ Chủ nhật) và chia đều tổng cũ để sửa tiếp.
  useEffect(() => {
    const d = detail.data;
    if (!open || !d) return;
    const rows: DailyRow[] = d.dailyTargets?.length
      ? d.dailyTargets.map((x) => ({ workDate: x.workDate, targetQty: x.targetQty }))
      : (() => {
          const dates = listWorkDays(d.startDate, d.endDate);
          const qty = evenSplit(d.targetFinishedQty, dates.length);
          return dates.map((workDate, i) => ({ workDate, targetQty: qty[i] ?? null }));
        })();
    form.setFieldValue('days', rows);
  }, [open, detail.data, form]);

  const orderOptions = useMemo(() => {
    const options = (orders.data?.items ?? []).map((o) => ({ value: o.orderId, label: o.orderCode }));
    // Đơn của chỉ tiêu đang sửa có thể đã ra khỏi danh sách "đơn đang chạy": vẫn phải hiện đúng mã.
    return target && !options.some((o) => o.value === target.orderId)
      ? [{ value: target.orderId, label: target.orderCode }, ...options]
      : options;
  }, [orders.data, target]);

  const productOptions = useMemo(() => {
    const seen = new Map<number, string>();
    (lines.data ?? []).forEach((l) => seen.set(l.productId, `${l.productCode} - ${l.productName}`));
    if (target && !seen.has(target.productId)) seen.set(target.productId, `${target.productCode} - ${target.productName}`);
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [lines.data, target]);

  const teamOptions = (teams.data ?? [])
    .filter((t) => t.teamType === 'Sewing')
    .map((t) => ({ value: t.teamId, label: t.teamName }));

  const required = [{ required: true, message: text.required }];

  const handleFinish = (values: FormValues) => {
    save.mutate(
      {
        orderId: values.orderId,
        productId: values.productId,
        sewingTeamId: values.sewingTeamId,
        startDate: toApiDate(values.period[0]),
        endDate: toApiDate(values.period[1]),
        dailyTargets: values.days.map((d) => ({ workDate: d.workDate, targetQty: d.targetQty ?? 0 })),
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={640}
      title={editing ? text.editTitle : text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        disabled={save.isPending}
        onValuesChange={(changed: Partial<FormValues>, all: FormValues) => {
          // Đổi kỳ: dựng lại các ngày (bỏ Chủ nhật), giữ số đã nhập của ngày vẫn còn trong kỳ.
          if (changed.period?.[0] && changed.period[1])
            form.setFieldValue('days', rowsForPeriod(toApiDate(changed.period[0]), toApiDate(changed.period[1]), all.days ?? []));
        }}
      >
        <Form.Item name="orderId" label={text.order} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            options={orderOptions}
            loading={orders.isLoading}
            disabled={editing}
            onChange={() => form.setFieldValue('productId', undefined)}
          />
        </Form.Item>
        <Form.Item name="productId" label={text.product} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            options={productOptions}
            loading={lines.isLoading}
            disabled={editing || orderId === null}
            placeholder={orderId === null ? text.noProduct : undefined}
          />
        </Form.Item>
        <Form.Item name="sewingTeamId" label={text.team} rules={required}>
          <Select options={teamOptions} loading={teams.isLoading} disabled={editing} />
        </Form.Item>
        <Form.Item name="period" label={text.period} rules={required}>
          <DatePicker.RangePicker format={DATE_FORMAT} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item
          name="days"
          label={text.dailyPlan}
          rules={[
            {
              validator: (_, rows: DailyRow[] | undefined) =>
                rows?.length && rows.every((r) => (r.targetQty ?? 0) > 0)
                  ? Promise.resolve()
                  : Promise.reject(new Error(text.dailyRequired)),
            },
          ]}
        >
          <KpiDailyPlanTable
            start={period?.[0] ? toApiDate(period[0]) : null}
            end={period?.[1] ? toApiDate(period[1]) : null}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
