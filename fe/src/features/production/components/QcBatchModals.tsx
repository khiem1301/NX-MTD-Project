import { useEffect } from 'react';
import { DatePicker, Form, Input, InputNumber, Modal, Select, TimePicker, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, formatQty, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT, SHIFT_OPTIONS } from '../constants';
import { useCreateQcBatch, useOnLineQty, useUpdateQcBatch } from '../hooks/useProduction';
import type { QcBatch, QcRole, ShiftCode } from '../types';

const text = PRODUCTION_TEXT.qc;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

type CreateValues = {
  workDate: Dayjs;
  shiftCode: ShiftCode;
  batchTime: Dayjs;
  sewingTeamId: number;
  orderId: number;
  orderLineId: number;
  finishedQty: number;
};

/** UC57 — QA/QC thêm đợt kiểm hoàn thiện. Số đạt không được vượt hàng đang có trên chuyền (BE kiểm). */
export function QcCreateModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<CreateValues>();
  const create = useCreateQcBatch();
  const teams = useTeamLookup();
  const orderId = Form.useWatch('orderId', form) ?? null;
  const teamId = Form.useWatch('sewingTeamId', form) ?? null;
  const lineId = Form.useWatch('orderLineId', form) ?? null;
  const lines = useOrderLineLookup(orderId);
  const onLine = useOnLineQty(teamId, lineId);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({ workDate: dayjs(), shiftCode: 'Afternoon', batchTime: dayjs() });
  }, [open, form]);

  const handleFinish = (values: CreateValues) => {
    create.mutate(
      {
        workDate: toApiDate(values.workDate),
        shiftCode: values.shiftCode,
        batchTime: `${values.batchTime.format('HH:mm')}:00`,
        sewingTeamId: values.sewingTeamId,
        orderLineId: values.orderLineId,
        finishedQty: values.finishedQty,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={560}
      title={text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={create.isPending}>
        <Form.Item name="workDate" label={text.form.date} rules={required}>
          <DatePicker format="DD/MM/YYYY" allowClear={false} />
        </Form.Item>
        <Form.Item name="shiftCode" label={text.form.shift} rules={required}>
          <Select options={SHIFT_OPTIONS} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="batchTime" label={text.form.time} rules={required}>
          <TimePicker format="HH:mm" allowClear={false} />
        </Form.Item>
        <Form.Item name="sewingTeamId" label={text.form.team} rules={required}>
          <Select
            loading={teams.isLoading}
            options={(teams.data ?? [])
              .filter((t) => t.teamType === 'Sewing')
              .map((t) => ({ value: t.teamId, label: t.teamName }))}
          />
        </Form.Item>
        <Form.Item name="orderId" label={PRODUCTION_TEXT.selectOrder} rules={required}>
          <OrderSelect width={460} onChange={() => form.setFieldValue('orderLineId', undefined)} />
        </Form.Item>
        <Form.Item name="orderLineId" label={text.form.line} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            disabled={orderId === null}
            loading={lines.isLoading}
            options={(lines.data ?? []).map((l) => ({
              value: l.orderLineId,
              label: `${l.productCode} - ${l.color} - ${l.sizeCode}`,
            }))}
          />
        </Form.Item>
        {onLine.data && (
          <Typography.Paragraph type="secondary">
            {`${text.onLine}: ${formatQty(onLine.data.onLine)} (${fill(
              text.onLineDetail,
              formatQty(onLine.data.received),
              formatQty(onLine.data.finished),
              formatQty(onLine.data.scrap),
              formatQty(onLine.data.onLine),
            )})`}
          </Typography.Paragraph>
        )}
        <Form.Item
          name="finishedQty"
          label={text.form.qty}
          rules={[...required, { type: 'number', min: 1, message: text.form.qtyMin }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 180 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}

type EditValues = { finishedQty: number; reason?: string };

/** Sửa số hàng đạt của một đợt. Sau hết ca chỉ Quản đốc sửa và phải ghi lý do (BE kiểm). */
export function QcEditModal({ role, batch, onClose }: { role: QcRole; batch: QcBatch | null; onClose: () => void }) {
  const [form] = Form.useForm<EditValues>();
  const update = useUpdateQcBatch(role);
  const needReason = role === 'manager' || (batch !== null && !batch.canEdit);

  useEffect(() => {
    if (batch) form.setFieldsValue({ finishedQty: batch.finishedQty, reason: undefined });
  }, [batch, form]);

  return (
    <Modal
      open={batch !== null}
      title={text.editTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) =>
          batch &&
          update.mutate(
            { id: batch.qcBatchId, body: { finishedQty: values.finishedQty, reason: values.reason?.trim() || undefined, rowVer: batch.rowVer } },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item
          name="finishedQty"
          label={text.form.qty}
          rules={[...required, { type: 'number', min: 1, message: text.form.qtyMin }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 180 }} />
        </Form.Item>
        {needReason && (
          <Form.Item
            name="reason"
            label={text.form.reason}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}
          >
            <Input.TextArea rows={2} maxLength={500} showCount />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
