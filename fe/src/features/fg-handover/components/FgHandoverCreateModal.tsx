import { useMemo, useState } from 'react';
import { Alert, Form, InputNumber, Modal, Select } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { FG_LABELS, FG_MESSAGES } from '../constants';
import { useCreateFgHandover, useFgHandoverable } from '../hooks/useFgHandover';
import type { FgHandoverableLine } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type FgHandoverCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = { qty: Record<string, number | null> };

/** UC44: QA/QC chọn đơn + mã hàng rồi gom số QC đạt chưa bàn giao; mặc định giao hết số còn lại, giao một phần được. */
export function FgHandoverCreateModal({ open, onClose }: FgHandoverCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const [orderId, setOrderId] = useState<number>();
  const [productId, setProductId] = useState<number>();
  const handoverable = useFgHandoverable(orderId);
  const create = useCreateFgHandover();

  const products = useMemo(() => {
    const seen = new Map<number, string>();
    (handoverable.data ?? []).forEach((l) => seen.set(l.productId, l.productCode));
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [handoverable.data]);
  // Chỉ có một mã hàng thì chọn sẵn, không bắt người dùng chọn thừa.
  const effectiveProductId = productId ?? (products.length === 1 ? products[0]?.value : undefined);
  const lines = (handoverable.data ?? []).filter((l) => l.productId === effectiveProductId);

  const handleFinish = ({ qty }: FormValues) => {
    if (orderId === undefined || effectiveProductId === undefined) return;
    const payload = Object.entries(qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ orderLineId: Number(id), handedQty: q ?? 0 }));
    create.mutate(
      { orderId, productId: effectiveProductId, lines: payload },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={FG_LABELS.create}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      okButtonProps={{ disabled: lines.length === 0 }}
      onOk={() => Modal.confirm({ title: FG_MESSAGES.confirmCreate, onOk: () => form.submit() })}
      onCancel={onClose}
      destroyOnHidden
    >
      <TableToolbar
        filters={
          <>
            <OrderSelect
              value={orderId}
              placeholder={FG_LABELS.selectOrder}
              onChange={(id) => {
                setOrderId(id);
                setProductId(undefined);
                form.resetFields();
              }}
            />
            {products.length > 1 && (
              <Select
                value={effectiveProductId}
                options={products}
                placeholder={FG_LABELS.selectProduct}
                style={{ width: 220 }}
                onChange={(id: number) => {
                  setProductId(id);
                  form.resetFields();
                }}
              />
            )}
          </>
        }
      />
      {orderId !== undefined &&
        !handoverable.isLoading &&
        lines.length > 0 &&
        lines.every((l) => l.remainingQty === 0) && (
          <Alert
            type="info"
            showIcon
            message={FG_LABELS.nothingToHand}
            style={{ marginBottom: 12 }}
          />
        )}
      <Form
        key={`${orderId}-${effectiveProductId}`}
        form={form}
        onFinish={handleFinish}
        disabled={create.isPending}
        initialValues={{
          qty: Object.fromEntries(lines.map((l) => [String(l.orderLineId), l.remainingQty])),
        }}
      >
        <DataTable<FgHandoverableLine>
          rowKey="orderLineId"
          size="small"
          pagination={false}
          loading={handoverable.isFetching}
          dataSource={lines}
          columns={[
            {
              title: FG_LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.color} - ${l.sizeCode}`,
            },
            { title: FG_LABELS.qcQty, dataIndex: 'qcQty', align: 'right' },
            { title: FG_LABELS.handedQty, dataIndex: 'handedQty', align: 'right' },
            { title: FG_LABELS.remainingQty, dataIndex: 'remainingQty', align: 'right' },
            {
              title: FG_LABELS.handoverQty,
              key: 'qty',
              render: (_, l) => (
                <Form.Item name={['qty', String(l.orderLineId)]} noStyle>
                  <InputNumber
                    min={0}
                    max={l.remainingQty}
                    precision={0}
                    disabled={l.remainingQty === 0}
                  />
                </Form.Item>
              ),
            },
          ]}
        />
      </Form>
    </Modal>
  );
}
