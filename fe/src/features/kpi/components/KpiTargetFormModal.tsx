import { Alert, Col, Descriptions, Form, InputNumber, Modal, Row, Select, Tag } from 'antd';
import dayjs from 'dayjs';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { fill, formatDate, formatQty } from '@/utils/format';
import { DELIVERING_ORDER_STATUSES, KPI_TEXT } from '../constants';
import type { KpiTarget } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { AppRangePicker } from '@/components/common/DateInputs';
import { useKpiTargetForm } from '../hooks/useKpiTargetForm';
import { KPI_MAX_QTY, periodLimitRule } from '../utils/periodRule';

export type Props = { open: boolean; target: KpiTarget | null; onClose: () => void };

const text = KPI_TEXT.form;

/** Nhãn số ngày còn tới ngày giao hàng. */
function DueTag({ dueDate }: { dueDate: string }) {
  const daysLeft = dayjs(dueDate).diff(dayjs().startOf('day'), 'day');
  if (daysLeft < 0) return <Tag color="red">{fill(text.dueOverdue, -daysLeft)}</Tag>;
  return daysLeft === 0 ? (
    <Tag color="orange">{text.dueToday}</Tag>
  ) : (
    <Tag>{fill(text.dueDaysLeft, daysLeft)}</Tag>
  );
}

/**
 * UC19 — Quản đốc lập / sửa chỉ tiêu KPI tổ may. Một đơn + mã hàng + tổ có nhiều chỉ tiêu nhưng các kỳ không trùng ngày (BE báo
 * nếu trùng); các chỉ tiêu cộng lại không vượt số lượng đơn đặt. Kỳ kết thúc sau hoặc sát ngày giao hàng chỉ cảnh báo, vẫn lưu
 * được. KPI từng ngày đặt riêng trên lịch KPI. Khi sửa, đơn / mã hàng / tổ không đổi được để khỏi lệch kết quả đã tính.
 */
export function KpiTargetFormModal({ open, target, onClose }: Props) {
  const {
    save,
    orderId,
    capacity,
    overCapacity,
    orderClosed,
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
  } = useKpiTargetForm({ open, target, onClose });

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={editing ? text.editTitle : text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      okButtonProps={{ disabled: overCapacity || orderClosed }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Row gutter={16}>
          <Col xs={24} md={12}>
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
          </Col>
          <Col xs={24} md={12}>
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
          </Col>
        </Row>
        {capacity && (
          <Descriptions
            size="small"
            bordered
            column={{ xs: 1, md: 2 }}
            style={{ marginBottom: 16 }}
            items={[
              {
                key: 'd',
                label: text.dueDate,
                children: capacity.dueDate && (
                  <>
                    <strong>{formatDate(capacity.dueDate)}</strong>{' '}
                    <DueTag dueDate={capacity.dueDate} />
                  </>
                ),
              },
              {
                key: 's',
                label: text.orderStatus,
                children: capacity.orderStatus && (
                  <StatusTag status={capacity.orderStatus} labels={ORDER_STATUS_TAG} />
                ),
              },
              { key: 'o', label: text.capacityOrderQty, children: formatQty(capacity.orderQty) },
              {
                key: 'a',
                label: text.capacityAssigned,
                children: formatQty(capacity.assignedToOthers),
              },
              {
                key: 'm',
                span: 'filled',
                label: text.capacityMax,
                children: <strong>{formatQty(capacity.maxTarget)}</strong>,
              },
            ]}
          />
        )}
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Form.Item name="sewingTeamId" label={text.team} rules={required}>
              <Select {...teamSelectProps} disabled={editing} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item
              name="period"
              label={text.period}
              rules={[...required, periodLimitRule(text.periodMax)]}
            >
              <AppRangePicker style={{ width: '100%' }} disabledDate={disabledDate} />
            </Form.Item>
          </Col>
        </Row>
        {orderClosed && (
          <Alert type="error" showIcon message={text.orderClosed} style={{ marginBottom: 16 }} />
        )}
        {DELIVERING_ORDER_STATUSES.includes(capacity?.orderStatus) && (
          <Alert
            type="warning"
            showIcon
            message={text.orderDelivering}
            style={{ marginBottom: 16 }}
          />
        )}
        {warning && capacity?.dueDate && (
          <Alert
            type="warning"
            showIcon
            message={fill(
              warning.kind === 'after' ? text.dueAfter : text.dueNear,
              formatDate(capacity.dueDate),
              warning.days,
            )}
            style={{ marginBottom: 16 }}
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
          rules={[
            ...required,
            { type: 'number', min: 1, message: text.qtyMin },
            { type: 'number', max: KPI_MAX_QTY, message: text.qtyMax },
          ]}
        >
          <InputNumber min={1} max={KPI_MAX_QTY} precision={0} style={{ width: 200 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
