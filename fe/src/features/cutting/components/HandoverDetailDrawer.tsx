import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Descriptions,
  Drawer,
  Form,
  Input,
  InputNumber,
  Popconfirm,
  Space,
  Spin,
  Table,
} from 'antd';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatDateTime } from '@/utils/format';
import { HANDOVER_STATUS, HANDOVER_STATUS_LABEL, LABELS } from '../constants';
import { useConfirmHandover, useHandover, useRejectHandover } from '../hooks/useCutting';
import type { ConfirmHandoverPayload, CutHandoverLine } from '../types';

type HandoverDetailDrawerProps = { handoverId: number | undefined; onClose: () => void };

/** Chi tiết phiếu bàn giao; Thủ kho đếm số thực nhận (lệch phải có lý do), nhận vào kho BTP hoặc từ chối. */
export function HandoverDetailDrawer({ handoverId, onClose }: HandoverDetailDrawerProps) {
  const { hasRole } = usePermission();
  const detail = useHandover(handoverId);
  const confirm = useConfirmHandover();
  const reject = useRejectHandover();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<ConfirmHandoverPayload>();
  const h = detail.data;
  const canReceive = hasRole(ROLES.WarehouseKeeper) && h?.status === HANDOVER_STATUS.PENDING;

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

  const actions = canReceive && h && (
    <Space>
      <Button danger onClick={() => setRejecting(true)}>
        {LABELS.reject}
      </Button>
      <Popconfirm title={MESSAGES.cutting.confirmHandover} onConfirm={() => form.submit()}>
        <Button type="primary" loading={confirm.isPending}>
          {LABELS.receive}
        </Button>
      </Popconfirm>
    </Space>
  );

  return (
    <Drawer
      open={handoverId !== undefined}
      onClose={onClose}
      width={820}
      title={h?.handoverCode}
      extra={actions}
    >
      {detail.isLoading && <Spin />}
      {detail.error && <Alert type="error" message={detail.error.message} />}
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
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label={LABELS.workOrder}>{h.workOrderCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.order}>{h.orderCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.cuttingTeam}>{h.cuttingTeamName}</Descriptions.Item>
            <Descriptions.Item label={LABELS.handedAt}>
              {formatDateTime(h.handedAt)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={h.status} labels={HANDOVER_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.confirmedBy}>
              {h.confirmedByName ? `${h.confirmedByName} · ${formatDateTime(h.confirmedAt)}` : '—'}
            </Descriptions.Item>
          </Descriptions>
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
            <Table<CutHandoverLine>
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
                        <InputNumber min={0} max={l.handedQty} precision={0} />
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
    </Drawer>
  );
}
