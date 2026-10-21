import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiError, ApiResponse } from '@/types/api';
import type { Attachment, AttachmentOwnerType, AttachmentPurpose } from '@/types/attachment';

const attachmentKeys = {
  list: (ownerType: AttachmentOwnerType, ownerId: number) => ['attachments', ownerType, ownerId] as const,
};

export const attachmentApi = {
  list: async (ownerType: AttachmentOwnerType, ownerId: number): Promise<Attachment[]> => {
    const res = await apiClient.get<ApiResponse<Attachment[]>>(API.attachments.base, { params: { ownerType, ownerId } });
    return res.data.data;
  },
  upload: async (
    ownerType: AttachmentOwnerType,
    ownerId: number,
    file: File,
    purpose: AttachmentPurpose,
  ): Promise<Attachment> => {
    const body = new FormData();
    body.append('ownerType', ownerType);
    body.append('ownerId', String(ownerId));
    body.append('purpose', purpose);
    body.append('file', file);
    const res = await apiClient.post<ApiResponse<Attachment>>(API.attachments.base, body);
    return res.data.data;
  },
  remove: async (attachmentId: number): Promise<void> => {
    await apiClient.delete(API.attachments.byId(attachmentId));
  },
  content: async (attachmentId: number): Promise<Blob> => {
    const res = await apiClient.get<Blob>(API.attachments.content(attachmentId), { responseType: 'blob' });
    return res.data;
  },
};

export const useAttachments = (ownerType: AttachmentOwnerType, ownerId: number | null) =>
  useQuery({
    queryKey: attachmentKeys.list(ownerType, ownerId ?? 0),
    queryFn: () => attachmentApi.list(ownerType, ownerId ?? 0),
    enabled: ownerId !== null,
  });

export const useUploadAttachment = (ownerType: AttachmentOwnerType, ownerId: number) => {
  const queryClient = useQueryClient();
  return useMutation<Attachment, ApiError, { file: File; purpose: AttachmentPurpose }>({
    mutationFn: ({ file, purpose }) => attachmentApi.upload(ownerType, ownerId, file, purpose),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: attachmentKeys.list(ownerType, ownerId) }),
  });
};

export const useRemoveAttachment = (ownerType: AttachmentOwnerType, ownerId: number) => {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, number>({
    mutationFn: attachmentApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: attachmentKeys.list(ownerType, ownerId) }),
  });
};

/** Ảnh cần token nên không dùng <img src=URL API> trực tiếp: tải blob rồi tạo object URL, tự thu hồi khi unmount. */
export const useAttachmentObjectUrl = (attachmentId: number): string | null => {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;
    attachmentApi
      .content(attachmentId)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => setUrl(null));
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [attachmentId]);
  return url;
};
