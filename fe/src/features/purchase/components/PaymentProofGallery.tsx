import { DeleteOutlined } from '@ant-design/icons';
import { Alert, Button, Empty, Image, Popconfirm, Space, Spin } from 'antd';
import { AttachmentUploadButton, AuthImage } from '@/components/common/attachments';
import { MESSAGES } from '@/constants/messages';
import { useAttachments, useRemoveAttachment } from '@/hooks/useAttachments';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { MAX_PROOF_PHOTOS } from '../constants';

type Props = { purchaseOrderId: number; canEdit: boolean };

/**
 * Ảnh chứng từ thanh toán của đơn mua (KH-07, OwnerType = PurchaseOrder, mục đích PaymentProof).
 * Làm riêng thay vì dùng AttachmentGallery chung vì thư viện chung chưa cho chọn mục đích ảnh khi tải lên.
 */
export function PaymentProofGallery({ purchaseOrderId, canEdit }: Props) {
  const ownerType = ATTACHMENT_OWNER.PurchaseOrder;
  const { data, isLoading, isError } = useAttachments(ownerType, purchaseOrderId);
  const remove = useRemoveAttachment(ownerType, purchaseOrderId);

  if (isLoading) return <Spin />;
  if (isError) return <Alert type="error" showIcon message={MESSAGES.common.loadError} />;

  const items = data ?? [];
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      {items.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={MESSAGES.attachments.empty} />
      )}
      <Image.PreviewGroup>
        <Space wrap>
          {items.map((a) => (
            <Space key={a.attachmentId} direction="vertical" align="center">
              <AuthImage attachmentId={a.attachmentId} alt={a.fileName ?? ''} />
              {canEdit && (
                <Popconfirm title={MESSAGES.attachments.removeConfirm} onConfirm={() => remove.mutate(a.attachmentId)}>
                  <Button size="small" danger icon={<DeleteOutlined />} aria-label={MESSAGES.attachments.remove} />
                </Popconfirm>
              )}
            </Space>
          ))}
        </Space>
      </Image.PreviewGroup>
      {canEdit && (
        <AttachmentUploadButton
          ownerType={ownerType}
          ownerId={purchaseOrderId}
          purpose={ATTACHMENT_PURPOSE.PaymentProof}
          remaining={MAX_PROOF_PHOTOS - items.length}
        />
      )}
    </Space>
  );
}
