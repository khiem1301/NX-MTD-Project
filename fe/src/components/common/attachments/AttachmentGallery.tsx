import { DeleteOutlined } from '@ant-design/icons';
import { Alert, Button, Empty, Image, Popconfirm, Space, Spin } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useAttachments, useRemoveAttachment } from '@/hooks/useAttachments';
import type { AttachmentOwnerType } from '@/types/attachment';
import { AttachmentUploadButton } from './AttachmentUploadButton';
import { AuthImage } from './AuthImage';

type AttachmentGalleryProps = { ownerType: AttachmentOwnerType; ownerId: number; canEdit: boolean; maxFiles: number };

/** Danh sách ảnh hiện tại của một chứng từ + nút gỡ / thêm khi canEdit. */
export function AttachmentGallery({ ownerType, ownerId, canEdit, maxFiles }: AttachmentGalleryProps) {
  const { data, isLoading, isError } = useAttachments(ownerType, ownerId);
  const remove = useRemoveAttachment(ownerType, ownerId);
  if (isLoading) return <Spin />;
  if (isError) return <Alert type="error" showIcon message={MESSAGES.common.loadError} />;
  const items = data ?? [];
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      {items.length === 0 && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={MESSAGES.attachments.empty} />}
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
      {canEdit && <AttachmentUploadButton ownerType={ownerType} ownerId={ownerId} remaining={maxFiles - items.length} />}
    </Space>
  );
}
