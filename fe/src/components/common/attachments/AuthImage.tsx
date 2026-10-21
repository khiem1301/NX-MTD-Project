import { Image, Skeleton } from 'antd';
import { useAttachmentObjectUrl } from '@/hooks/useAttachments';

type AuthImageProps = { attachmentId: number; alt: string; size?: number };

const DEFAULT_SIZE = 96;

export function AuthImage({ attachmentId, alt, size = DEFAULT_SIZE }: AuthImageProps) {
  const url = useAttachmentObjectUrl(attachmentId);
  if (!url) return <Skeleton.Image active style={{ width: size, height: size }} />;
  return <Image src={url} alt={alt} width={size} height={size} style={{ objectFit: 'cover' }} />;
}
