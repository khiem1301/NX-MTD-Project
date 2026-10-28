import { useState } from 'react';
import {
  Alert,
  Button,
  Descriptions,
  Drawer,
  Popconfirm,
  Space,
  Spin,
  Table,
  Typography,
} from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import type { FabricRequirement } from '@/types/workOrder';
import { formatDate, formatQty } from '@/utils/format';
import { LABELS, WORK_ORDER_STATUS, WORK_ORDER_STATUS_LABEL } from '../constants';
import { NoteModal } from '@/components/common/NoteModal';
import { useCancelWorkOrder, useFinishCutting, useWorkOrder } from '../hooks/useCutting';
import type { WorkOrder, WorkOrderLine } from '../types';

type WorkOrderDetailDrawerProps = {
  workOrderId: number | undefined;
  onClose: () => void;
  onEdit: (workOrder: WorkOrder) => void;
};

/** Chi tiết lệnh cắt: kế hoạch / đã cắt / đã bàn giao theo màu - size và vải cần theo định mức. */
export function WorkOrderDetailDrawer({
  workOrderId,
  onClose,
  onEdit,
}: WorkOrderDetailDrawerProps) {
  const { hasRole } = usePermission();
  const detail = useWorkOrder(workOrderId);
  const cancel = useCancelWorkOrder();
  const finish = useFinishCutting();
  const [finishing, setFinishing] = useState(false);
  const w = detail.data;
  const canEdit = hasRole(ROLES.Manager) && w?.status === WORK_ORDER_STATUS.OPEN;
  const total = (pick: (l: WorkOrderLine) => number) => (w?.lines ?? []).reduce((sum, l) => sum + pick(l), 0);
  // Tổ cắt báo cắt xong khi lệnh đang cắt (đủ kế hoạch thì lệnh đã tự hoàn thành khi kho xác nhận).
  const canFinish = hasRole(ROLES.TeamLeader, ROLES.TeamDeputy) && w?.status === WORK_ORDER_STATUS.CUTTING;
  const isShort = total((l) => l.handedQty) < total((l) => l.plannedQty);

  const actions = canFinish && w ? (
    <Button type="primary" onClick={() => setFinishing(true)}>
      {LABELS.finishCutting}
    </Button>
  ) : canEdit && w && (
    <Space>
      <Popconfirm
        title={MESSAGES.cutting.confirmCancelWorkOrder}
        onConfirm={() => cancel.mutate(w.workOrderId, { onSuccess: onClose })}
      >
        <Button danger>{LABELS.cancelWorkOrder}</Button>
      </Popconfirm>
      <Button onClick={() => onEdit(w)}>{MESSAGES.common.edit}</Button>
    </Space>
  );

  return (
    <Drawer
      open={workOrderId !== undefined}
      onClose={onClose}
      width={860}
      title={w?.workOrderCode}
      extra={actions}
    >
      <NoteModal
        open={finishing}
        title={LABELS.finishCutting}
        label={isShort ? LABELS.finishCuttingReason : LABELS.finishCuttingHint}
        required={isShort}
        loading={finish.isPending}
        onCancel={() => setFinishing(false)}
        onSubmit={(note) =>
          w &&
          finish.mutate({ id: w.workOrderId, reason: note ?? undefined }, { onSuccess: () => { setFinishing(false); onClose(); } })
        }
      />
      {detail.isLoading && <Spin />}
      {detail.error && <Alert type="error" message={detail.error.message} />}
      {w && (
        <>
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label={LABELS.order}>{w.orderCode}</Descriptions.Item>
            <Descriptions.Item
              label={LABELS.product}
            >{`${w.productCode} ${w.productName}`}</Descriptions.Item>
            <Descriptions.Item label={LABELS.cuttingTeam}>{w.cuttingTeamName}</Descriptions.Item>
            <Descriptions.Item label={LABELS.dueDate}>{formatDate(w.dueDate)}</Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={w.status} labels={WORK_ORDER_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.note}>{w.note ?? '—'}</Descriptions.Item>
          </Descriptions>
          <Alert
            style={{ marginTop: 16 }}
            type="info"
            showIcon
            message={`${LABELS.result}: ${LABELS.resultSummary
              .replace('{planned}', formatQty(total((l) => l.plannedQty)))
              .replace('{cut}', formatQty(total((l) => l.cutQty)))
              .replace('{handed}', formatQty(total((l) => l.handedQty)))}`}
          />
          <Table<WorkOrderLine>
            rowKey="workOrderLineId"
            size="small"
            style={{ marginTop: 16 }}
            pagination={false}
            dataSource={w.lines}
            columns={[
              {
                title: LABELS.orderLine,
                key: 'line',
                render: (_, l) => `${l.color} - ${l.sizeCode}`,
              },
              {
                title: LABELS.component,
                dataIndex: 'componentName',
                render: (v: string) => v || LABELS.wholeGarment,
              },
              { title: LABELS.plannedQty, dataIndex: 'plannedQty', align: 'right' },
              { title: LABELS.cutQty, dataIndex: 'cutQty', align: 'right' },
              { title: LABELS.handedQty, dataIndex: 'handedQty', align: 'right' },
            ]}
          />
          <Typography.Title level={5} style={{ marginTop: 16 }}>
            {LABELS.fabrics}
          </Typography.Title>
          <Table<FabricRequirement>
            rowKey="materialId"
            size="small"
            pagination={false}
            dataSource={w.fabrics}
            columns={[
              {
                title: LABELS.material,
                key: 'm',
                render: (_, f) => `${f.materialCode} ${f.materialName}${f.materialColor ? ` (${f.materialColor})` : ''}`,
              },
              {
                title: LABELS.requiredQty,
                dataIndex: 'requiredQty',
                align: 'right',
                render: formatQty,
              },
              {
                title: LABELS.issuedQty,
                dataIndex: 'issuedQty',
                align: 'right',
                render: formatQty,
              },
              {
                title: LABELS.remainingQty,
                dataIndex: 'remainingQty',
                align: 'right',
                render: formatQty,
              },
              {
                title: LABELS.availableQty,
                dataIndex: 'availableQty',
                align: 'right',
                render: formatQty,
              },
            ]}
          />
        </>
      )}
    </Drawer>
  );
}
