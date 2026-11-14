import { useState } from 'react';
import { Alert, Button, Form, Modal, Space, Spin } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { LABELS, SHORTAGE_OPEN_STATUS, SHORTAGE_RESOLUTION } from '../constants';
import { useResolveShortDelivery, useShortDelivery } from '../hooks/useShortDeliveries';
import type { ShortDeliveryLine } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';
import { shortDeliveryColumns } from './shortDeliveryColumns';
import { ShortDeliveryInfo } from './ShortDeliveryInfo';
import { ReplenishPlanForm } from './ReplenishPlanForm';
import { toReplenishPlan, type ReplenishPlanValues } from '../utils/replenishPlan';
import { remainingReplenish } from '../utils/supplementaryPayload';

type ShortDeliveryModalProps = {
  requestId: number | undefined;
  onClose: () => void;
};

/**
 * UC08 / UC39: chi tiết đơn thiếu NVL khi nhập. Quản đốc chọn giao bù (nhập số bù từng dòng) hoặc chốt không bù;
 * Thủ kho chỉ xem; nhận hàng bù (lập phiếu bổ sung) ở tab "Chờ giao bù" của màn NVL chờ nhận.
 */
export function ShortDeliveryModal({ requestId, onClose }: ShortDeliveryModalProps) {
  const { hasRole } = usePermission();
  const detail = useShortDelivery(requestId);
  const resolve = useResolveShortDelivery();
  // Số bù Quản đốc đã sửa; dòng chưa sửa mặc định bù đủ phần còn thiếu. Trang gắn key theo đơn nên state tự reset.
  const [overrides, setOverrides] = useState<Record<number, number>>({});
  const [planForm] = Form.useForm<ReplenishPlanValues>();
  const d = detail.data;
  const isManager = hasRole(ROLES.Admin, ROLES.Manager);
  const open = d?.status === SHORTAGE_OPEN_STATUS;
  const unresolved = open && d?.resolution === null;
  // Tiêu đề theo bước đang làm: chờ Quản đốc xử lý hoặc chỉ xem.
  const modalTitle = unresolved ? LABELS.shortageResolveTitle : LABELS.shortagesTitle;

  // Đơn được trả về sau khi giao bù chưa đủ: số bù đợt này tính trên phần chưa nhận bù.
  const unreplenished = (l: ShortDeliveryLine) => Math.max(0, l.shortfallQty - l.replenishedQty);
  const replenishOf = (l: ShortDeliveryLine) => overrides[l.requestLineId] ?? unreplenished(l);
  // Số bù vượt phần còn thiếu đã báo lỗi ở dòng; còn dòng vượt thì không cho chọn giao bù.
  const hasOver = (d?.lines ?? []).some((l) => replenishOf(l) > unreplenished(l));

  const columns = shortDeliveryColumns({
    canEditReplenish: unresolved && isManager,
    canReceive: false,
    replenishOf,
    remaining: remainingReplenish,
    onReplenishChange: (line, qty) => setOverrides({ ...overrides, [line.requestLineId]: qty }),
  });

  const submitReplenish = async () => {
    if (!d) return;
    const plan = await planForm.validateFields().catch(() => null);
    if (!plan) return;
    resolve.mutate(
      {
        id: d.requestId,
        body: {
          resolution: SHORTAGE_RESOLUTION.REPLENISH,
          lines: d.lines.map((l) => ({
            requestLineId: l.requestLineId,
            replenishQty: replenishOf(l),
          })),
          ...toReplenishPlan(plan),
        },
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={requestId !== undefined}
      width={MODAL_WIDTH.lg}
      title={d ? `${modalTitle} — ${d.requestCode}` : modalTitle}
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
                <Button
                  type="primary"
                  loading={resolve.isPending}
                  disabled={hasOver}
                  onClick={() => void submitReplenish()}
                >
                  {LABELS.replenish}
                </Button>
              </>
            )}
          </Space>
        )
      }
    >
      {detail.isLoading && <Spin />}
      {detail.error && <Alert type="error" message={detail.error.message} />}
      {d && (
        <>
          <ShortDeliveryInfo detail={d} />
          <DataTable
            rowKey="requestLineId"
            columns={columns}
            dataSource={d.lines}
            pagination={false}
          />
        </>
      )}
      {d && unresolved && isManager && <ReplenishPlanForm form={planForm} />}
    </Modal>
  );
}
