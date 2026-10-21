import { useEffect, useState } from 'react';
import { Image } from 'antd';
import { productApi } from '../api/orderApi';

const isRemoteUrl = (v: string) => /^https?:\/\//i.test(v);

/** Ảnh mẫu: đường dẫn http(s) (dữ liệu cũ) dùng trực tiếp; khóa ảnh tải từ máy cần token nên tải blob rồi tạo object URL. */
export function ProductImage({ value, size = 40 }: { value: string; size?: number }) {
  const [blobUrl, setBlobUrl] = useState<{ key: string; url: string } | null>(null);
  const remote = isRemoteUrl(value);

  useEffect(() => {
    if (remote) return;
    let objectUrl: string | null = null;
    let cancelled = false;
    productApi
      .sampleImageBlob(value)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setBlobUrl({ key: value, url: objectUrl });
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [value, remote]);

  const src = remote ? value : blobUrl?.key === value ? blobUrl.url : undefined;
  if (!src) return <div style={{ width: size, height: size, background: '#f0f0f0' }} />;
  return <Image src={src} width={size} height={size} style={{ objectFit: 'cover' }} fallback="" />;
}
