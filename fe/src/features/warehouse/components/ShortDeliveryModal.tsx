import { useState } from 'react';
import { Alert, Button, Form, Input, Modal, Space, Spin } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { API_DATE } from '@/utils/format';
import { LABELS, SHORTAGE_RESOLUTION } from '../constants';
import {
  useCreateSupplementary,
  useResolveShortDelivery,
  useShortDelivery,
} from '../hooks/useShortDeliveries';
import type { ShortDeliveryLine } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';
import { shortDeliveryColumns } from './shortDeliveryColumns';
import { ShortDeliveryInfo } from './ShortDeliveryInfo';

type ShortDeliveryModalProps = {
  requestId: number | undefined;
  onClose: () => void;
};

const OPEN_STATUS = 'PENDING_PROCESSING';

type SupplementaryValues = {
  receiptDate: Dayjs;
  note?: string;
  lines: Record<number, { acceptedQty: number }>;
};

/**
 * UC08 / UC39: chi tiết đơn thiếu NVL khi nhập. Quản đốc chọn giao bù (nhập số bù từng dòng) hoặc chốt không bù;
 * Thủ kho lập phiếu bổ sung khi hàng bù về (số nhận ≤ số còn phải bù).
 */
export function ShortDeliveryModal({ requestId, onClose }: ShortDeliveryModalProps) {
  const { hasRole } = usePermission();
  const detail = useShortDelivery(requestId);
  const resolve = useResolveShortDelivery();
  const supplementary = useCreateSupplementary();
  // Số bù Quản đốc đã sửa; dòng chưa sửa mặc định bù đủ phần còn thiếu. Trang gắn key theo đơn nên state tự reset.
  const [overrides, setOverrides] = useState<Record<number, number>>({});
  const [form] = Form.useForm<SupplementaryValues>();
  const d = detail.data;
  const isManager = hasRole(ROLES.Manager);
  const isKeeper = hasRole(ROLES.WarehouseKeeper);
  const open = d?.status === OPEN_STATUS;
  const unresolved = open && d?.resolution === null;
  const awaitingReplenish = open && d?.resolution === SHORTAGE_RESOLUTION.REPLENISH;

  const replenishOf = (l: ShortDeliveryLine) => overrides[l.requestLineId] ?? l.shortfallQty;
  const remaining = (l: ShortDeliveryLine) => Math.max(0, (l.replenishQty ?? 0) - l.replenishedQty);

  const columns = shortDeliveryColumns({
    canEditReplenish: unresolved && isManager,
    canReceive: awaitingReplenish && isKeeper,
    replenishOf,
    remaining,
    onReplenishChange: (line, qty) => setOverrides({ ...overrides, [line.requestLineId]: qty }),
  });

  const submitReplenish = () => {
    if (!d) return;
    resolve.mutate(
      {
        id: d.requestId,
        body: {
          resolution: SHORTAGE_RESOLUTION.REPLENISH,
          lines: d.lines.map((l) => ({
            requestLineId: l.requestLineId,
            replenishQty: replenishOf(l),
          })),
        },
      },
      { onSuccess: onClose },
    );
  };

  const submitSupplementary = (values: SupplementaryValues) => {
    if (!d) return;
    const lines = d.lines
      .filter((l) => remaining(l) > 0)
      .map((l) => {
        const input = values.lines[l.materialReceiptLineId];
        return {
          originalReceiptLineId: l.materialReceiptLineId,
          acceptedQty: input?.acceptedQty ?? 0,
        };
      })
      .filter((l) => l.acceptedQty > 0);
    supplementary.mutate(
      {
        shortageRequestId: d.requestId,
        receiptDate: values.receiptDate.format(API_DATE),
        note: values.note,
        lines,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={requestId !== undefined}
      width={MODAL_WIDTH.lg}
      title={d ? `${LABELS.shortagesTitle} — ${d.requestCode}` : LABELS.shortagesTitle}
      onCancel={onClose}
      destroyOnHidden
      footer={
        d && (
          <Space>
            <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
            {unresolved && isManager && (
              <>
                <ConfirmAction
                  title={MESSAGES.warehouse.confirmNoReplenish}
                  onConfirm={() =>
                    resolve.mutate(
                      { id: d.requestId, body: { resolution: SHORTAGE_RESOLUTION.NO_REPLENISH } },
                      { onSuccess: onClose },
                    )
                  }
                >
                  <Button danger loading={resolve.isPending}>
                    {LABELS.noReplenish}
                  </Button>
                </ConfirmAction>
                <Button type="primary" loading={resolve.isPending} onClick={submitReplenish}>
                  {LABELS.replenish}
                </Button>
              </>
            )}
            {awaitingReplenish && isKeeper && (
              <Button
                type="primary"
                loading={supplementary.isPending}
                onClick={() => form.submit()}
              >
                {LABELS.createSupplementary}
              </Button>
            )}
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
          onFinish={submitSupplementary}
          disabled={supplementary.isPending}
        >
          <ShortDeliveryInfo detail={d} />
          <DataTable
            rowKey="requestLineId"
            columns={columns}
            dataSource={d.lines}
            pagination={false}
          />
          {awaitingReplenish && isKeeper && (
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
          )}
        </Form>
      )}
    </Modal>
  );
}
