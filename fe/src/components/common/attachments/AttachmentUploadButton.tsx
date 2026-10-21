import { UploadOutlined } from '@ant-design/icons';
import { Button, Upload, message, type UploadProps } from 'antd';
import { ATTACHMENT_ACCEPT, MAX_ATTACHMENT_BYTES } from '@/constants/attachments';
import { MESSAGES } from '@/constants/messages';
import { useUploadAttachment } from '@/hooks/useAttachments';
import type { AttachmentOwnerType, AttachmentPurpose } from '@/types/attachment';

type AttachmentUploadButtonProps = {
  ownerType: AttachmentOwnerType;
  ownerId: number;
  remaining: number;
  purpose?: AttachmentPurpose;
  disabled?: boolean;
};

export function AttachmentUploadButton({
  ownerType,
  ownerId,
  remaining,
  purpose = 'Photo',
  disabled,
}: AttachmentUploadButtonProps) {
  const upload = useUploadAttachment(ownerType, ownerId);
  const isDisabled = disabled === true || remaining <= 0;

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (file.size > MAX_ATTACHMENT_BYTES) {
      void message.error(MESSAGES.attachments.tooLarge);
      return Upload.LIST_IGNORE;
    }
    return true;
  };

  const customRequest: UploadProps['customRequest'] = ({ file, onSuccess, onError }) => {
    if (!(file instanceof File)) return;
    upload.mutate(
      { file, purpose },
      {
        onSuccess: (data) => {
          onSuccess?.(data);
          void message.success(MESSAGES.attachments.uploaded);
        },
        onError: (error) => {
          onError?.(new Error(error.message));
          void message.error(error.message);
        },
      },
    );
  };

  return (
    <Upload
      accept={ATTACHMENT_ACCEPT}
      showUploadList={false}
      beforeUpload={beforeUpload}
      customRequest={customRequest}
      disabled={isDisabled}
    >
      <Button icon={<UploadOutlined />} disabled={isDisabled} loading={upload.isPending}>
        {`${MESSAGES.attachments.add} (${Math.max(remaining, 0)})`}
      </Button>
    </Upload>
  );
}
