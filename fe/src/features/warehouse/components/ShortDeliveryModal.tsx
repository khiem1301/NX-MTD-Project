import { useState } from 'react';
import {
  Alert,
  Button,
  Descriptions,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Space,
  Spin,
  type TableColumnsType,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { API_DATE, formatDateTime, formatQty } from '@/utils/format';
import {
  LABELS,
  SHORTAGE_RESOLUTION,
  SHORTAGE_RESOLUTION_LABEL,
  SHORTAGE_STATUS_LABEL,
} from '../constants';
import {
  useCreateSupplementary,
  useResolveShortDelivery,
  useShortDelivery,
} from '../hooks/useShortDeliveries';
import type { ShortDeliveryLine } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';

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

  const columns: TableColumnsType<ShortDeliveryLine> = [
    { title: LABELS.materialCode, dataIndex: 'materialCode' },
    { title: LABELS.materialName, dataIndex: 'materialName' },
    { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
    { title: LABELS.unit, dataIndex: 'unit' },
    { title: LABELS.shortfallQty, dataIndex: 'shortfallQty', align: 'right', render: formatQty },
    ...(unresolved && isManager
      ? [
          {
            title: LABELS.replenishQty,
            key: 'replenish',
            render: (_: unknown, l: ShortDeliveryLine) => (
              <InputNumber
                min={0}
                max={l.shortfallQty}
                value={replenishOf(l)}
                onChange={(v) => setOverrides({ ...overrides, [l.requestLineId]: v ?? 0 })}
              />
            ),
          },
        ]
      : [
          {
            title: LABELS.replenishQty,
            dataIndex: 'replenishQty',
            align: 'right' as const,
            render: (v: number | null) => (v === null ? '—' : formatQty(v)),
          },
          {
            title: LABELS.replenishedQty,
            dataIndex: 'replenishedQty',
            align: 'right' as const,
            render: formatQty,
          },
        ]),
    ...(awaitingReplenish && isKeeper
      ? [
          {
            title: LABELS.acceptedQty,
            key: 'accepted',
            render: (_: unknown, l: ShortDeliveryLine) => (
              <Space>
                <Form.Item
                  name={['lines', l.materialReceiptLineId, 'acceptedQty']}
                  noStyle
                  initialValue={remaining(l)}
                >
                  <InputNumber
                    min={0}
                    max={remaining(l)}
                    addonBefore={LABELS.acceptedQty}
                    style={{ width: 150 }}
                  />
                </Form.Item>
              </Space>
            ),
          },
        ]
      : []),
  ];

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
                <Popconfirm
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
                </Popconfirm>
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
          <Descriptions column={2} size="small" bordered style={{ marginBottom: 16 }}>
            <Descriptions.Item label={LABELS.receiptCode}>{d.receiptCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={d.status} labels={SHORTAGE_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.order}>{d.orderCode ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={LABELS.purchaseOrder}>{d.poCode ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={LABELS.shortageResolution}>
              {d.resolution ? (
                <StatusTag status={d.resolution} labels={SHORTAGE_RESOLUTION_LABEL} />
              ) : (
                '—'
              )}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.submittedAt}>
              {formatDateTime(d.createdAt)}
            </Descriptions.Item>
            {d.resultNote && (
              <Descriptions.Item label={LABELS.note} span={2}>
                {d.resultNote}
              </Descriptions.Item>
            )}
          </Descriptions>
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
