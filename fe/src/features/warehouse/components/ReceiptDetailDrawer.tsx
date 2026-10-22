import { useState } from 'react';
import {
  Alert,
  Button,
  Descriptions,
  Divider,
  Drawer,
  Popconfirm,
  Space,
  Spin,
  Table,
  type TableColumnsType,
} from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { formatDate, formatDateTime, formatQty } from '@/utils/format';
import { LABELS, RECEIPT_STATUS, RECEIPT_STATUS_LABEL } from '../constants';
import { useConfirmReceipt, useReceipt, useReturnReceipt } from '../hooks/useReceipts';
import type { MaterialReceipt, MaterialReceiptLine } from '../types';

const MAX_RECEIPT_PHOTOS = 5;

type ReceiptDetailDrawerProps = {
  receiptId: number | undefined;
  onClose: () => void;
  onEdit: (receipt: MaterialReceipt) => void;
};

const lineColumns: TableColumnsType<MaterialReceiptLine> = [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
  { title: LABELS.acceptedQty, dataIndex: 'acceptedQty', align: 'right', render: formatQty },
  { title: LABELS.shortQty, dataIndex: 'shortQty', align: 'right', render: formatQty },
  { title: LABELS.defectiveQty, dataIndex: 'defectiveQty', align: 'right', render: formatQty },
];

/** Chi tiết phiếu nhập; Quản đốc xác nhận / trả lại (UC39), Thủ kho sửa phiếu chờ hoặc bị trả. */
export function ReceiptDetailDrawer({ receiptId, onClose, onEdit }: ReceiptDetailDrawerProps) {
  const { hasRole } = usePermission();
  const receipt = useReceipt(receiptId);
  const confirm = useConfirmReceipt();
  const giveBack = useReturnReceipt();
  const [returning, setReturning] = useState(false);
  const r = receipt.data;
  const pending = r?.status === RECEIPT_STATUS.PENDING;
  const editable = pending || r?.status === RECEIPT_STATUS.RETURNED;

  const actions = r && (
    <Space>
      {hasRole(ROLES.Manager) && pending && (
        <>
          <Button danger onClick={() => setReturning(true)}>
            {LABELS.returnReceipt}
          </Button>
          <Popconfirm
            title={MESSAGES.warehouse.confirmReceipt}
            onConfirm={() => confirm.mutate(r.materialReceiptId, { onSuccess: onClose })}
          >
            <Button type="primary" loading={confirm.isPending}>
              {LABELS.confirm}
            </Button>
          </Popconfirm>
        </>
      )}
      {hasRole(ROLES.WarehouseKeeper) && editable && (
        <Button onClick={() => onEdit(r)}>{MESSAGES.common.edit}</Button>
      )}
    </Space>
  );

  return (
    <Drawer
      open={receiptId !== undefined}
      onClose={onClose}
      width={820}
      title={r?.receiptCode}
      extra={actions}
    >
      {receipt.isLoading && <Spin />}
      {receipt.error && <Alert type="error" message={receipt.error.message} />}
      {r && (
        <>
          {r.returnReason && (
            <Alert
              type="warning"
              showIcon
              message={`${LABELS.returnReason}: ${r.returnReason}`}
              style={{ marginBottom: 16 }}
            />
          )}
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label={LABELS.order}>{r.orderCode}</Descriptions.Item>
            <Descriptions.Item label={LABELS.status}>
              <StatusTag status={r.status} labels={RECEIPT_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.receiptDate}>
              {formatDate(r.receiptDate)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.submittedAt}>
              {formatDateTime(r.submittedAt)}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.confirmedBy}>
              {r.confirmedByName ? `${r.confirmedByName} · ${formatDateTime(r.confirmedAt)}` : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={LABELS.note}>{r.note ?? '—'}</Descriptions.Item>
          </Descriptions>
          <Table
            rowKey="materialReceiptLineId"
            size="small"
            style={{ marginTop: 16 }}
            columns={lineColumns}
            dataSource={r.lines}
            pagination={false}
          />
          <Divider orientation="left">{LABELS.documentPhotos}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.MaterialReceipt}
            ownerId={r.materialReceiptId}
            canEdit={hasRole(ROLES.WarehouseKeeper) && editable}
            maxFiles={MAX_RECEIPT_PHOTOS}
            purpose={ATTACHMENT_PURPOSE.DocumentPhoto}
          />
          <ReasonModal
            open={returning}
            title={LABELS.returnTitle}
            loading={giveBack.isPending}
            onCancel={() => setReturning(false)}
            onSubmit={(reason) =>
              giveBack.mutate(
                { id: r.materialReceiptId, reason },
                {
                  onSuccess: () => {
                    setReturning(false);
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
