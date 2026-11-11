import { Alert, Button, Descriptions, Form, Input, Modal, Space, Spin } from 'antd';
import dayjs from 'dayjs';
import { DataTable } from '@/components/common/DataTable';
import { DetailDescriptions } from '@/components/common/DetailModal';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { formatDeliveryWindow } from '@/utils/expectedDelivery';
import { LABELS } from '../constants';
import { useCreateSupplementary, useShortDelivery } from '../hooks/useShortDeliveries';
import {
  remainingReplenish,
  toSupplementaryPayload,
  type SupplementaryValues,
} from '../utils/supplementaryPayload';
import { shortDeliveryColumns } from './shortDeliveryColumns';

type ReplenishReceiveModalProps = {
  requestId: number | undefined;
  onClose: () => void;
};

/**
 * Thủ kho nhận hàng bù cho đơn thiếu đã chọn giao bù: chỉ bảng NVL với ô số nhận, ngày nhập, ghi chú và nút Lập phiếu
 * bổ sung. Xem đầy đủ trạng thái, cách xử lý của đơn thiếu ở màn Thiếu NVL.
 */
export function ReplenishReceiveModal({ requestId, onClose }: ReplenishReceiveModalProps) {
  const detail = useShortDelivery(requestId);
  const supplementary = useCreateSupplementary();
  const [form] = Form.useForm<SupplementaryValues>();
  const d = detail.data;

  const columns = shortDeliveryColumns({
    canEditReplenish: false,
    canReceive: true,
    replenishOf: () => 0,
    remaining: remainingReplenish,
    onReplenishChange: () => undefined,
  });

  return (
    <Modal
      open={requestId !== undefined}
      width={MODAL_WIDTH.lg}
      title={d ? `${LABELS.shortageReceiveTitle} — ${d.requestCode}` : LABELS.shortageReceiveTitle}
      onCancel={onClose}
      destroyOnHidden
      footer={
        d && (
          <Space>
            <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
            <Button type="primary" loading={supplementary.isPending} onClick={() => form.submit()}>
              {LABELS.createSupplementary}
            </Button>
          </Space>
        )
      }
    >
      {detail.isLoading && <Spin />}
      {detail.error && <Alert type="error" message={detail.error.message} />}
      {d && (
        <Form
          form={form}
          layout="vertical"
          initialValues={{ receiptDate: dayjs() }}
          onFinish={(values) =>
            supplementary.mutate(toSupplementaryPayload(d, values), { onSuccess: onClose })
          }
          disabled={supplementary.isPending}
        >
          <DetailDescriptions style={{ marginBottom: 16 }}>
            <Descriptions.Item label={LABELS.receiptCode}>{d.receiptCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.purchaseOrder}>{d.poCode ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={LABELS.order}>{d.orderCode ?? '—'}</Descriptions.Item>
            {!!d.expectedDeliveryCount && (
              <Descriptions.Item label={LABELS.replenishPlan}>
                {fill(
                  LABELS.replenishPlanValue,
                  d.replenishDeliveryCount ?? 0,
                  d.expectedDeliveryCount,
                  formatDeliveryWindow(d.expectedDeliveryDate, d.expectedDeliveryDateTo),
                )}
              </Descriptions.Item>
            )}
          </DetailDescriptions>
          <DataTable
            rowKey="requestLineId"
            columns={columns}
            dataSource={d.lines.filter((l) => remainingReplenish(l) > 0)}
            pagination={false}
          />
          <Space style={{ marginTop: 16 }} align="start" wrap>
            <Form.Item
              name="receiptDate"
              label={LABELS.receiptDate}
              rules={[{ required: true, message: MESSAGES.common.required }]}
            >
              <AppDatePicker />
            </Form.Item>
            <Form.Item name="note" label={LABELS.note}>
              <Input.TextArea rows={1} maxLength={1000} style={{ width: 420 }} />
            </Form.Item>
          </Space>
        </Form>
      )}
    </Modal>
  );
}
