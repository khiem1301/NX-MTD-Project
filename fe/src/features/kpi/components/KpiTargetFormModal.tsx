import { Alert, Descriptions, Divider, Form, InputNumber, Modal, Select, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import type { KpiTarget } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppRangePicker } from '@/components/common/DateInputs';
import { useKpiTargetForm } from '../hooks/useKpiTargetForm';

export type Props = { open: boolean; target: KpiTarget | null; onClose: () => void };

const text = KPI_TEXT.form;

/**
 * UC19 — Quản đốc lập / sửa chỉ tiêu KPI tổ may theo đơn hàng + mã hàng + tổ may + kỳ. Mã hàng chọn trong số mã hàng
 * của đơn đã chọn; BE kiểm lại và báo trùng kỳ. Khi sửa, đơn / mã hàng / tổ không đổi được để khỏi lệch kết quả đã tính.
 */
export function KpiTargetFormModal({ open, target, onClose }: Props) {
  const {
    save,
    orderId,
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
  } = useKpiTargetForm({ open, target, onClose });

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.sm}
      title={editing ? text.editTitle : text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      okButtonProps={{ disabled: overCapacity }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Divider orientation="left" plain style={{ marginTop: 0 }}>
          {text.sectionOrder}
        </Divider>
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
        <Divider orientation="left" plain>
          {text.sectionTeam}
        </Divider>
        <Form.Item name="sewingTeamId" label={text.team} rules={required}>
          <Select options={teamOptions} loading={teams.isLoading} disabled={editing} />
        </Form.Item>
        <Form.Item name="period" label={text.period} rules={required}>
          <AppRangePicker style={{ width: '100%' }} />
        </Form.Item>
        <Divider orientation="left" plain>
          {text.sectionTarget}
        </Divider>
        {capacity && (
          <Descriptions
            size="small"
            bordered
            column={1}
            title={text.capacityTitle}
            style={{ marginBottom: 16 }}
            items={[
              {
                key: 'w',
                label: text.capacityWarehouse,
                children: formatQty(capacity.warehouseBtp),
              },
              { key: 't', label: text.capacityTeamHeld, children: formatQty(capacity.teamHeld) },
              {
                key: 'c',
                label: text.capacityCommitted,
                children: formatQty(capacity.committedByOthers),
              },
              {
                key: 'm',
                label: text.capacityMax,
                children: <strong>{formatQty(capacity.maxTarget)}</strong>,
              },
            ]}
          />
        )}
        {overCapacity && (
          <Alert
            type="error"
            showIcon
            message={text.capacityExceeds}
            style={{ marginBottom: 16 }}
          />
        )}
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
