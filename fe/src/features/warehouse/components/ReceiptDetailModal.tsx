import { useState } from 'react';
import { PrinterOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { Alert, Button, Divider, Space } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { AttachmentGallery } from '@/components/common/attachments';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { useAttachments } from '@/hooks/useAttachments';
import { usePermission } from '@/hooks/usePermission';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { LABELS, MAX_RECEIPT_PHOTOS, RECEIPT_STATUS } from '../constants';
import {
  useConfirmReceipt,
  useDownloadReceipt,
  useReceipt,
  useReturnReceipt,
} from '../hooks/useReceipts';
import type { MaterialReceipt } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { ReceiptInfo } from './ReceiptInfo';
import { receiptLineColumns } from './receiptLineColumns';

type ReceiptDetailModalProps = {
  receiptId: number | undefined;
  onClose: () => void;
  onEdit: (receipt: MaterialReceipt) => void;
};

/** Chi tiết phiếu nhập; Quản đốc xác nhận / trả lại (UC39), Thủ kho sửa phiếu chờ hoặc bị trả. */
export function ReceiptDetailModal({ receiptId, onClose, onEdit }: ReceiptDetailModalProps) {
  const { hasRole } = usePermission();
  const receipt = useReceipt(receiptId);
  const confirm = useConfirmReceipt();
  const giveBack = useReturnReceipt();
  const download = useDownloadReceipt();
  const [returning, setReturning] = useState(false);
  const r = receipt.data;
  // Quản đốc chỉ xác nhận khi phiếu có ảnh chứng từ giao hàng (BE cũng chặn).
  const photos = useAttachments(ATTACHMENT_OWNER.MaterialReceipt, r?.materialReceiptId ?? null);
  const missingPhoto = photos.isSuccess && photos.data.length === 0;
  const pending = r?.status === RECEIPT_STATUS.PENDING;
  // Phiếu bổ sung không sửa được: nếu bị trả lại thì Thủ kho lập phiếu bổ sung mới.
  const editable = r?.receiptType === 'NEW' && (pending || r?.status === RECEIPT_STATUS.RETURNED);

  const actions = r && (
    <Space>
      <Button
        icon={<PrinterOutlined />}
        loading={download.isPending}
        onClick={() => download.mutate({ id: r.materialReceiptId, code: r.receiptCode })}
      >
        {MESSAGES.common.printExcel}
      </Button>
      {hasRole(ROLES.WarehouseKeeper) && editable && (
        <Button onClick={() => onEdit(r)}>{MESSAGES.common.edit}</Button>
      )}
      {hasRole(ROLES.Admin, ROLES.Manager) && pending && (
        <>
          <Button danger onClick={() => setReturning(true)}>
            {LABELS.returnReceipt}
          </Button>
          <ConfirmAction
            title={MESSAGES.warehouse.confirmReceipt}
            onConfirm={() => confirm.mutate(r.materialReceiptId, { onSuccess: onClose })}
          >
            <Button type="primary" loading={confirm.isPending} disabled={missingPhoto}>
              {LABELS.confirm}
            </Button>
          </ConfirmAction>
        </>
      )}
    </Space>
  );

  return (
    <DetailModal
      open={receiptId !== undefined}
      onClose={onClose}
      title={r?.receiptCode}
      extra={actions}
      loading={receipt.isLoading}
      error={receipt.error}
    >
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
          {pending && missingPhoto && (
            <Alert
              type="warning"
              showIcon
              message={MESSAGES.warehouse.receiptPhotoMissing}
              style={{ marginBottom: 16 }}
            />
          )}
          {r.shortDeliveryRequestId !== null && (
            <Alert
              type="info"
              showIcon
              message={
                <>
                  {MESSAGES.warehouse.shortageStaged}{' '}
                  <Link to={ROUTES.shortDeliveries}>{LABELS.openShortage}</Link>
                </>
              }
              style={{ marginBottom: 16 }}
            />
          )}
          <ReceiptInfo receipt={r} />
          <DataTable
            rowKey="materialReceiptLineId"
            style={{ marginTop: 16 }}
            columns={receiptLineColumns}
            dataSource={r.lines}
            pagination={false}
          />
          <Divider orientation="left">{LABELS.documentPhotos}</Divider>
          <AttachmentGallery
            ownerType={ATTACHMENT_OWNER.MaterialReceipt}
            ownerId={r.materialReceiptId}
            canEdit={false}
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
    </DetailModal>
  );
}
