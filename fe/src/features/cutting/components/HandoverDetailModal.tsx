import { useEffect, useState } from 'react';
import { Alert, Button, Form, Input, InputNumber, Space } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { HANDOVER_STATUS, LABELS } from '../constants';
import {
  useCancelHandover,
  useConfirmHandover,
  useHandover,
  useRejectHandover,
} from '../hooks/useCutting';
import type { ConfirmHandoverPayload, CutHandoverLine } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { HandoverInfo } from './HandoverInfo';

type HandoverDetailModalProps = { handoverId: number | undefined; onClose: () => void };

/** Chi tiết phiếu bàn giao; Thủ kho đếm số thực nhận (lệch phải có lý do), nhận vào kho BTP hoặc từ chối. */
export function HandoverDetailModal({ handoverId, onClose }: HandoverDetailModalProps) {
  const { hasRole } = usePermission();
  const detail = useHandover(handoverId);
  const confirm = useConfirmHandover();
  const reject = useRejectHandover();
  const cancel = useCancelHandover();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<ConfirmHandoverPayload>();
  const h = detail.data;
  const canReceive = hasRole(ROLES.WarehouseKeeper) && h?.status === HANDOVER_STATUS.PENDING;
  // Tổ cắt rút phiếu của tổ mình khi Thủ kho chưa nhận (BE kiểm tổ).
  const canCancel =
    (hasRole(ROLES.TeamLeader) || hasRole(ROLES.TeamDeputy)) &&
    h?.status === HANDOVER_STATUS.PENDING;

  useEffect(() => {
    if (h)
      form.setFieldsValue({
        lines: h.lines.map((l) => ({
          cutHandoverLineId: l.cutHandoverLineId,
          confirmedQty: l.handedQty,
        })),
      });
  }, [h, form]);

  const reasonRule = (index: number, handed: number) => ({
    validator: async (_rule: unknown, value?: string) => {
      const qty = form.getFieldValue(['lines', index, 'confirmedQty']) as number | undefined;
      if (qty !== handed && !value?.trim())
        throw new Error(MESSAGES.warehouse.discrepancyReasonRequired);
    },
  });

  const cancelAction = canCancel && h && (
    <ConfirmAction
      title={MESSAGES.cutting.confirmCancelHandover}
      onConfirm={() => cancel.mutate(h.cutHandoverId, { onSuccess: onClose })}
    >
      <Button danger loading={cancel.isPending}>
        {LABELS.cancelHandover}
      </Button>
    </ConfirmAction>
  );

  const actions =
    cancelAction ||
    (canReceive && h && (
      <Space>
        <Button danger onClick={() => setRejecting(true)}>
          {LABELS.reject}
        </Button>
        <ConfirmAction title={MESSAGES.cutting.confirmHandover} onConfirm={() => form.submit()}>
          <Button type="primary" loading={confirm.isPending}>
            {LABELS.receive}
          </Button>
        </ConfirmAction>
      </Space>
    ));

  return (
    <DetailModal
      open={handoverId !== undefined}
      onClose={onClose}
      title={h?.handoverCode}
      extra={actions}
      loading={detail.isLoading}
      error={detail.error}
    >
      {h && (
        <>
          {h.rejectReason && (
            <Alert
              type="warning"
              showIcon
              message={`${LABELS.rejectReason}: ${h.rejectReason}`}
              style={{ marginBottom: 16 }}
            />
          )}
          <HandoverInfo handover={h} />
          <Form
            form={form}
            disabled={confirm.isPending}
            style={{ marginTop: 16 }}
            onFinish={(values) =>
              confirm.mutate({ id: h.cutHandoverId, body: values }, { onSuccess: onClose })
            }
          >
            {canReceive &&
              h.lines.map((l, index) => (
                <Form.Item
                  key={l.cutHandoverLineId}
                  name={['lines', index, 'cutHandoverLineId']}
                  hidden
                >
                  <InputNumber />
                </Form.Item>
              ))}
            <DataTable<CutHandoverLine>
              rowKey="cutHandoverLineId"
              size="small"
              pagination={false}
              dataSource={h.lines}
              columns={[
                {
                  title: LABELS.orderLine,
                  key: 'line',
                  render: (_, l) =>
                    `${l.productCode} ${l.color} - ${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`,
                },
                { title: LABELS.handoverQty, dataIndex: 'handedQty', align: 'right' },
                {
                  title: LABELS.confirmedQty,
                  key: 'confirmed',
                  render: (_, l, index) =>
                    canReceive ? (
                      <Form.Item
                        name={['lines', index, 'confirmedQty']}
                        noStyle
                        rules={[{ required: true, message: MESSAGES.common.required }]}
                      >
                        <QtyInput min={0} max={l.handedQty} />
                      </Form.Item>
                    ) : (
                      (l.confirmedQty ?? '—')
                    ),
                },
                {
                  title: LABELS.discrepancyReason,
                  key: 'reason',
                  render: (_, l, index) =>
                    canReceive ? (
                      <Form.Item
                        name={['lines', index, 'discrepancyReason']}
                        dependencies={[['lines', index, 'confirmedQty']]}
                        rules={[reasonRule(index, l.handedQty)]}
                        style={{ marginBottom: 0 }}
                      >
                        <Input maxLength={500} />
                      </Form.Item>
                    ) : (
                      (l.discrepancyReason ?? '—')
                    ),
                },
              ]}
            />
          </Form>
          <ReasonModal
            open={rejecting}
            title={LABELS.rejectTitle}
            loading={reject.isPending}
            onCancel={() => setRejecting(false)}
            onSubmit={(reason) =>
              reject.mutate(
                { id: h.cutHandoverId, reason },
                {
                  onSuccess: () => {
                    setRejecting(false);
                    onClose();
                  },
                },
              )
            }
          />
        </>
      )}
    </DetailModal>
  );
}
