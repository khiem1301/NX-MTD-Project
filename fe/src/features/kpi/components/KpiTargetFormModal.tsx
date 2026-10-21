import { useEffect, useMemo } from 'react';
import { DatePicker, Form, InputNumber, Modal, Select, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineLookup, useOrderLookup } from '@/hooks/useOrderLookups';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { formatQty, toApiDate } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useSaveKpiTarget } from '../hooks/useKpi';
import type { KpiTarget } from '../types';

type Props = { open: boolean; target: KpiTarget | null; onClose: () => void };

type FormValues = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  period: [Dayjs, Dayjs];
  targetFinishedQty: number;
  workingDays: number;
};

const DATE_FORMAT = 'DD/MM/YYYY';
const text = KPI_TEXT.form;

/**
 * UC19 — Quản đốc lập / sửa chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ. Mã hàng chọn trong số mã hàng
 * của đơn đã chọn; BE kiểm lại và báo trùng kỳ. Khi sửa, đơn / mã hàng / tổ không đổi được để khỏi lệch kết quả đã tính.
 */
export function KpiTargetFormModal({ open, target, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveKpiTarget(target?.kpiTargetId ?? null);
  const orderId = Form.useWatch('orderId', form) ?? null;
  const qty = Form.useWatch('targetFinishedQty', form);
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

  return (
    <Modal
      open={open}
      width={560}
      title={editing ? text.editTitle : text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
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
          name="targetFinishedQty"
          label={text.targetFinishedQty}
          rules={[...required, { type: 'number', min: 1, message: text.qtyMin }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 200 }} />
        </Form.Item>
        <Form.Item
          name="workingDays"
          label={text.workingDays}
          rules={[...required, { type: 'number', min: 1, message: text.daysMin }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 200 }} />
        </Form.Item>
        {dailyPreview !== null && (
          <Typography.Text type="secondary">{`${text.dailyPreview}: ${formatQty(dailyPreview)}`}</Typography.Text>
        )}
      </Form>
    </Modal>
  );
}
