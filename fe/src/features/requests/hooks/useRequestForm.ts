import { Form, message, type UploadFile } from 'antd';
import dayjs from 'dayjs';
import { useState } from 'react';
import { MESSAGES } from '@/constants/messages';
import { attachmentApi } from '@/hooks/useAttachments';
import type { ApiError } from '@/types/api';
import { applyFieldErrors } from '@/utils/apiError';
import { fill, toApiDate } from '@/utils/format';
import { GENERIC_FORM_TYPES } from '../constants';
import { useCreateRequest, useRequestTypes, useUpdateRequest } from './useRequests';
import type { RequestFormModalProps } from '../components/RequestFormModal';
import { type Dayjs } from 'dayjs';
import type { RequestType } from '@/constants/requestTypes';

/** Trạng thái và xử lý của RequestFormModal; component chỉ lo giao diện. */
export function useRequestForm(props: Pick<RequestFormModalProps, 'editing' | 'onSaved'>) {
  const { editing, onSaved } = props;
  const [form] = Form.useForm<FormValues>();
  const [files, setFiles] = useState<UploadFile[]>([]);
  const { data: types } = useRequestTypes();
  const create = useCreateRequest();
  const update = useUpdateRequest(editing?.requestId ?? 0);
  const options = (types ?? []).filter((t) => GENERIC_FORM_TYPES.includes(t.requestType));

  // Ảnh cần OwnerId nên chỉ tải được sau khi đơn đã có Id; lỗi một phần không làm mất đơn.
  const uploadPhotos = async (requestId: number) => {
    const results = await Promise.allSettled(
      files.map((f) =>
        attachmentApi.upload('Request', requestId, f.originFileObj as File, 'Photo'),
      ),
    );
    const rejected = results.filter((r): r is PromiseRejectedResult => r.status === 'rejected');
    if (rejected.length > 0)
      void message.warning(
        `${fill(MESSAGES.requests.photoUploadFailed, rejected.length)} ${(rejected[0]?.reason as ApiError | undefined)?.message ?? ''}`.trim(),
      );
  };

  const handleFinish = async (values: FormValues) => {
    const body = {
      title: values.title ?? null,
      content: values.content ?? null,
      targetDate: values.targetDate ? toApiDate(values.targetDate) : null,
      lines: (values.lines ?? []).map((l) => ({
        materialId: l.materialId ?? null,
        itemDescription: l.materialId ? null : l.itemDescription,
        quantity: l.quantity,
      })),
    };
    try {
      if (editing) {
        await update.mutateAsync({ ...body, rowVer: editing.rowVer });
        onSaved(editing.requestId);
      } else {
        const newId = await create.mutateAsync({ ...body, requestType: values.requestType });
        await uploadPhotos(newId);
        void message.success(MESSAGES.requests.created);
        onSaved(newId);
      }
      form.resetFields();
      setFiles([]);
    } catch (e) {
      // useApiMutation đã hiện message; lỗi 400 theo field thì gắn thêm vào ô nhập.
      applyFieldErrors(form, e as ApiError);
    }
  };

  const initialValues: Partial<FormValues> = editing
    ? {
        requestType: editing.requestType,
        title: editing.title ?? undefined,
        content: editing.content ?? undefined,
        targetDate: editing.targetDate ? dayjs(editing.targetDate) : undefined,
        lines: editing.lines.map((l) => ({
          materialId: l.materialId ?? undefined,
          itemDescription: l.itemDescription ?? '',
          quantity: l.quantity,
        })),
      }
    : { lines: [EMPTY_LINE] };

  return {
    create,
    update,
    options,
    handleFinish,
    initialValues,
    form,
    files,
    setFiles,
  };
}

type FormValues = {
  requestType: RequestType;
  title?: string;
  content?: string;
  targetDate?: Dayjs;
  lines?: { materialId?: number; itemDescription?: string; quantity: number }[];
};

export const EMPTY_LINE = { itemDescription: '', quantity: 1 };
