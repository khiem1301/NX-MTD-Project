import { useState } from 'react';
import { Alert, Button, Descriptions, Form, Input, InputNumber, Popconfirm } from 'antd';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatDateTime } from '@/utils/format';
import { FG_LABELS, FG_MESSAGES, FG_STATUS, FG_STATUS_LABEL } from '../constants';
import {
  useCancelFgHandover,
  useConfirmFgHandover,
  useRejectFgHandover,
} from '../hooks/useFgHandover';
import type { FgHandover, FgHandoverLine } from '../types';
import { DetailDescriptions, ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type FormValues = { counts: Record<string, { confirmedQty: number; discrepancyReason?: string }> };

export function FgHandoverModalBody({
  handover: h,
  onClose,
}: {
  handover: FgHandover;
  onClose: () => void;
}) {
  const { hasRole } = usePermission();
  const confirm = useConfirmFgHandover();
  const reject = useRejectFgHandover();
  const cancel = useCancelFgHandover();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const pending = h.status === FG_STATUS.PENDING;
  const canReceive = hasRole(ROLES.WarehouseKeeper) && pending;
  const canCancel = hasRole(ROLES.QaQc) && pending;

  const reasonRule = (lineId: number, handed: number) => ({
    validator: async (_rule: unknown, value?: string) => {
      const qty = form.getFieldValue(['counts', String(lineId), 'confirmedQty']) as
        number | undefined;
      if (qty !== handed && !value?.trim()) throw new Error(FG_MESSAGES.discrepancyReasonRequired);
    },
  });

  const handleFinish = ({ counts }: FormValues) =>
    confirm.mutate(
      {
        id: h.fgHandoverId,
        body: {
          lines: h.lines.map((l) => ({
            fgHandoverLineId: l.fgHandoverLineId,
            confirmedQty: counts[String(l.fgHandoverLineId)]?.confirmedQty ?? l.handedQty,
            discrepancyReason: counts[String(l.fgHandoverLineId)]?.discrepancyReason,
          })),
        },
      },
      { onSuccess: onClose },
    );

  return (
    <>
      <ModalActions>
        {canReceive && (
          <>
            <Button danger onClick={() => setRejecting(true)}>
              {FG_LABELS.reject}
            </Button>
            <Popconfirm title={FG_MESSAGES.confirmReceive} onConfirm={() => form.submit()}>
              <Button type="primary" loading={confirm.isPending}>
                {FG_LABELS.receive}
              </Button>
            </Popconfirm>
          </>
        )}
        {canCancel && (
          <Popconfirm
            title={FG_MESSAGES.confirmCancel}
            onConfirm={() => cancel.mutate(h.fgHandoverId, { onSuccess: onClose })}
          >
            <Button danger loading={cancel.isPending}>
              {FG_LABELS.cancel}
            </Button>
          </Popconfirm>
        )}
      </ModalActions>
      {h.rejectReason && (
        <Alert
          type="warning"
          showIcon
          message={`${FG_LABELS.rejectReason}: ${h.rejectReason}`}
          style={{ marginBottom: 16 }}
        />
      )}
      <DetailDescriptions>
        <Descriptions.Item label={FG_LABELS.order}>{h.orderCode}</Descriptions.Item>
        <Descriptions.Item label={FG_LABELS.product}>{h.productCode}</Descriptions.Item>
        <Descriptions.Item label={FG_LABELS.inspector}>{h.inspectorName}</Descriptions.Item>
        <Descriptions.Item label={FG_LABELS.handedAt}>
          {formatDateTime(h.handedAt)}
        </Descriptions.Item>
        <Descriptions.Item label={FG_LABELS.status}>
          <StatusTag status={h.status} labels={FG_STATUS_LABEL} />
        </Descriptions.Item>
        <Descriptions.Item label={FG_LABELS.confirmedBy}>
          {h.confirmedByName ? `${h.confirmedByName} · ${formatDateTime(h.confirmedAt)}` : '—'}
        </Descriptions.Item>
      </DetailDescriptions>
      <Form
        form={form}
        disabled={confirm.isPending}
        style={{ marginTop: 16 }}
        initialValues={{
          counts: Object.fromEntries(
            h.lines.map((l) => [String(l.fgHandoverLineId), { confirmedQty: l.handedQty }]),
          ),
        }}
        onFinish={handleFinish}
      >
        <DataTable<FgHandoverLine>
          rowKey="fgHandoverLineId"
          size="small"
          pagination={false}
          dataSource={h.lines}
          columns={[
            {
              title: FG_LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.color} - ${l.sizeCode}`,
            },
            { title: FG_LABELS.totalHandedQty, dataIndex: 'handedQty', align: 'right' },
            {
              title: FG_LABELS.confirmedQty,
              key: 'confirmed',
              render: (_, l) =>
                canReceive ? (
                  <Form.Item
                    name={['counts', String(l.fgHandoverLineId), 'confirmedQty']}
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
              title: FG_LABELS.discrepancyReason,
              key: 'reason',
              render: (_, l) =>
                canReceive ? (
                  <Form.Item
                    name={['counts', String(l.fgHandoverLineId), 'discrepancyReason']}
                    dependencies={[['counts', String(l.fgHandoverLineId), 'confirmedQty']]}
                    rules={[reasonRule(l.fgHandoverLineId, l.handedQty)]}
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
        title={FG_LABELS.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(false)}
        onSubmit={(reason) =>
          reject.mutate(
            { id: h.fgHandoverId, reason },
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
  );
}
