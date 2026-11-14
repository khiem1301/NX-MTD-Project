import { useEffect, useState } from 'react';
import { Alert, Form, Input, Modal, Segmented, message } from 'antd';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { OrderSelect } from '@/components/common/OrderSelect';
import { WorkOrderSelect } from '@/components/common/WorkOrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useAuth } from '@/hooks/useAuth';
import { RETURN_KIND, RETURN_KIND_LABEL, RETURN_LABELS, RETURN_MESSAGES } from '../constants';
import { useCreateReturn, useReturnable, useReturnWorkOrders } from '../hooks/useMaterialReturn';
import type { ReturnWarehouse } from '../types';
import {
  buildReturnLines,
  returnableQueryOf,
  suggestedQtys,
  type ScrapRow,
} from '../utils/returnLines';
import { ReturnableTable } from './ReturnableTable';
import { ScrapLines } from './ScrapLines';

type ReturnCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = { qty: Record<string, number | null>; scrap?: ScrapRow[]; note?: string };

const KIND_OPTIONS = Object.values(RETURN_KIND).map((v) => ({
  value: v,
  label: RETURN_KIND_LABEL[v],
}));

/**
 * UC42 (DEV-ALL-112): tổ cắt trả NVL theo lệnh cắt; tổ may trả NVL (không theo đơn) hoặc BTP (theo đơn – mã – màu – size, cả bộ).
 * Cả hai tổ chọn thêm NVL phế phẩm (không có trần). Trần số trả: số còn trả được của từng hàng.
 */
export function ReturnCreateModal({ open, onClose }: ReturnCreateModalProps) {
  const { user } = useAuth();
  const isCutting = user?.teamType === 'Cutting';
  const [form] = Form.useForm<FormValues>();
  const [kind, setKind] = useState<ReturnWarehouse>(RETURN_KIND.MATERIAL);
  const [workOrderId, setWorkOrderId] = useState<number>();
  const [orderId, setOrderId] = useState<number>();
  const workOrders = useReturnWorkOrders(open && isCutting);
  const create = useCreateReturn();

  const isMaterial = kind === RETURN_KIND.MATERIAL;
  const query = returnableQueryOf({ kind, isCutting, workOrderId, orderId });
  const returnable = useReturnable(query);
  const lines = returnable.data ?? [];
  const canSuggest = isCutting && lines.length > 0 && lines.every((l) => l.usedQty !== null);

  // Điền sẵn số vải còn lại sau cắt khi danh sách của lệnh cắt về.
  useEffect(() => {
    form.setFieldValue('qty', suggestedQtys(returnable.data ?? [], isCutting));
  }, [returnable.data, isCutting, form]);

  const handleKindChange = (next: string | number) => {
    setKind(next === RETURN_KIND.SEMI_FINISHED ? RETURN_KIND.SEMI_FINISHED : RETURN_KIND.MATERIAL);
    form.setFieldsValue({ qty: {}, scrap: [] });
  };

  const handleFinish = ({ qty, scrap, note }: FormValues) => {
    const payload = buildReturnLines(qty, isMaterial ? scrap : undefined, lines);
    if (payload.length === 0) return void message.warning(RETURN_LABELS.nothingEntered);
    create.mutate(
      {
        warehouseType: kind,
        workOrderId: isMaterial && isCutting ? workOrderId : undefined,
        orderId: isMaterial ? undefined : orderId,
        note,
        lines: payload,
      },
      { onSuccess: onClose },
    );
  };

  const hint = isCutting
    ? canSuggest
      ? RETURN_LABELS.suggestHint
      : RETURN_LABELS.noSuggestHint
    : isMaterial
      ? RETURN_LABELS.materialHintSewing
      : RETURN_LABELS.btpHint;

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.lg}
      title={RETURN_LABELS.create}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() =>
        Modal.confirm({ title: RETURN_MESSAGES.confirmCreate, onOk: () => form.submit() })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      {!isCutting && (
        <Segmented
          value={kind}
          options={KIND_OPTIONS}
          onChange={handleKindChange}
          aria-label={RETURN_LABELS.kind}
          style={{ marginBottom: 12 }}
        />
      )}
      <div style={{ marginBottom: 12 }}>
        {isMaterial && isCutting && (
          <WorkOrderSelect
            width="100%"
            workOrders={workOrders.data}
            loading={workOrders.isLoading}
            value={workOrderId}
            placeholder={RETURN_LABELS.workOrderPlaceholder}
            onChange={setWorkOrderId}
          />
        )}
        {!isMaterial && (
          <OrderSelect
            width="100%"
            value={orderId}
            placeholder={RETURN_LABELS.orderPlaceholder}
            onChange={setOrderId}
          />
        )}
      </div>
      <Form form={form} layout="vertical" disabled={create.isPending} onFinish={handleFinish}>
        {query === undefined && (
          <Alert
            type="info"
            showIcon
            message={isMaterial ? RETURN_LABELS.pickWorkOrder : RETURN_LABELS.pickOrder}
            style={{ marginBottom: 12 }}
          />
        )}
        {query !== undefined && !returnable.isLoading && lines.length === 0 && (
          <Alert
            type="info"
            showIcon
            message={RETURN_LABELS.nothingToReturn}
            style={{ marginBottom: 12 }}
          />
        )}
        {lines.length > 0 && (
          <>
            <Alert type="info" showIcon message={hint} style={{ marginBottom: 12 }} />
            <ReturnableTable
              lines={lines}
              usedTitle={
                isCutting ? RETURN_LABELS.usedCut : isMaterial ? null : RETURN_LABELS.usedQc
              }
            />
          </>
        )}
        {isMaterial && (
          <div style={{ marginTop: 16 }}>
            <ScrapLines />
          </div>
        )}
        <Form.Item name="note" label={RETURN_LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
