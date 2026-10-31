import { useState } from 'react';
import { Alert, Button, Descriptions, Form, Input, InputNumber, Popconfirm } from 'antd';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatDateTime, formatQty } from '@/utils/format';
import { RETURN_LABELS, RETURN_MESSAGES, RETURN_STATUS, RETURN_STATUS_LABEL } from '../constants';
import {
  useCancelReturn,
  useConfirmReturn,
  useMaterialReturn,
  useRejectReturn,
} from '../hooks/useMaterialReturn';
import type { MaterialReturn, MaterialReturnLine } from '../types';
import { DetailModal, DetailDescriptions, ModalActions } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type ReturnModalProps = { returnId: number | undefined; onClose: () => void };
type FormValues = { counts: Record<string, { confirmedQty: number; discrepancyReason?: string }> };

/** Chi tiết phiếu trả vải: Thủ kho đếm (lệch phải có lý do), nhận vào kho hoặc từ chối; tổ cắt rút phiếu chờ. */
export function ReturnModal({ returnId, onClose }: ReturnModalProps) {
  const detail = useMaterialReturn(returnId);
  const r = detail.data;
  return (
    <DetailModal
      open={returnId !== undefined}
      onClose={onClose}
      title={r?.returnCode}
      loading={detail.isLoading}
      error={detail.error}
    >
      {r && <DrawerBody key={r.materialReturnId} ret={r} onClose={onClose} />}
    </DetailModal>
  );
}

function DrawerBody({ ret: r, onClose }: { ret: MaterialReturn; onClose: () => void }) {
  const { hasRole } = usePermission();
  const confirm = useConfirmReturn();
  const reject = useRejectReturn();
  const cancel = useCancelReturn();
  const [rejecting, setRejecting] = useState(false);
  const [form] = Form.useForm<FormValues>();
  const pending = r.status === RETURN_STATUS.PENDING;
  const canReceive = hasRole(ROLES.WarehouseKeeper) && pending;
  const canCancel = hasRole(ROLES.TeamLeader, ROLES.TeamDeputy) && pending;

  const reasonRule = (lineId: number, returned: number) => ({
    validator: async (_rule: unknown, value?: string) => {
      const qty = form.getFieldValue(['counts', String(lineId), 'confirmedQty']) as
        number | undefined;
      if (qty !== returned && !value?.trim())
        throw new Error(RETURN_MESSAGES.discrepancyReasonRequired);
    },
  });

  const handleFinish = ({ counts }: FormValues) =>
    confirm.mutate(
      {
        id: r.materialReturnId,
        body: {
          lines: r.lines.map((l) => ({
            materialReturnLineId: l.materialReturnLineId,
            confirmedQty: counts[String(l.materialReturnLineId)]?.confirmedQty ?? l.returnedQty,
            discrepancyReason: counts[String(l.materialReturnLineId)]?.discrepancyReason,
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
              {RETURN_LABELS.reject}
            </Button>
            <Popconfirm title={RETURN_MESSAGES.confirmReceive} onConfirm={() => form.submit()}>
              <Button type="primary" loading={confirm.isPending}>
                {RETURN_LABELS.receive}
              </Button>
            </Popconfirm>
          </>
        )}
        {canCancel && (
          <Popconfirm
            title={RETURN_MESSAGES.confirmCancel}
            onConfirm={() => cancel.mutate(r.materialReturnId, { onSuccess: onClose })}
          >
            <Button danger loading={cancel.isPending}>
              {RETURN_LABELS.cancel}
            </Button>
          </Popconfirm>
        )}
      </ModalActions>
      {r.rejectReason && (
        <Alert
          type="warning"
          showIcon
          message={`${RETURN_LABELS.rejectReason}: ${r.rejectReason}`}
          style={{ marginBottom: 16 }}
        />
      )}
      <DetailDescriptions>
        <Descriptions.Item label={RETURN_LABELS.issue}>{r.issueCode}</Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.order}>{r.orderCode}</Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.team}>{r.cuttingTeamName}</Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.createdAt}>
          {formatDateTime(r.createdAt)}
        </Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.status}>
          <StatusTag status={r.status} labels={RETURN_STATUS_LABEL} />
        </Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.confirmedBy}>
          {r.confirmedByName ? `${r.confirmedByName} · ${formatDateTime(r.confirmedAt)}` : '—'}
        </Descriptions.Item>
        <Descriptions.Item label={RETURN_LABELS.note} span={2}>
          {r.note ?? '—'}
        </Descriptions.Item>
      </DetailDescriptions>
      <Form
        form={form}
        disabled={confirm.isPending}
        style={{ marginTop: 16 }}
        initialValues={{
          counts: Object.fromEntries(
            r.lines.map((l) => [String(l.materialReturnLineId), { confirmedQty: l.returnedQty }]),
          ),
        }}
        onFinish={handleFinish}
      >
        <DataTable<MaterialReturnLine>
          rowKey="materialReturnLineId"
          size="small"
          pagination={false}
          dataSource={r.lines}
          columns={[
            {
              title: RETURN_LABELS.material,
              key: 'material',
              render: (_, l) =>
                `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''}`,
            },
            { title: RETURN_LABELS.unit, dataIndex: 'unit' },
            {
              title: RETURN_LABELS.totalQty,
              dataIndex: 'returnedQty',
              align: 'right',
              render: formatQty,
            },
            {
              title: RETURN_LABELS.confirmedQty,
              key: 'confirmed',
              render: (_, l) =>
                canReceive ? (
                  <Form.Item
                    name={['counts', String(l.materialReturnLineId), 'confirmedQty']}
                    noStyle
                    rules={[{ required: true, message: MESSAGES.common.required }]}
                  >
                    <InputNumber min={0} max={l.returnedQty} />
                  </Form.Item>
                ) : l.confirmedQty === null ? (
                  '—'
                ) : (
                  formatQty(l.confirmedQty)
                ),
            },
            {
              title: RETURN_LABELS.discrepancyReason,
              key: 'reason',
              render: (_, l) =>
                canReceive ? (
                  <Form.Item
                    name={['counts', String(l.materialReturnLineId), 'discrepancyReason']}
                    dependencies={[['counts', String(l.materialReturnLineId), 'confirmedQty']]}
                    rules={[reasonRule(l.materialReturnLineId, l.returnedQty)]}
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
        title={RETURN_LABELS.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(false)}
        onSubmit={(reason) =>
          reject.mutate(
            { id: r.materialReturnId, reason },
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
