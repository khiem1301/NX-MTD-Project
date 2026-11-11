import { message } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { MESSAGES } from '@/constants/messages';
import { ERROR_CODES } from '@/constants/errorCodes';
import type { ApiError } from '@/types/api';
import { useRequestAction, useRequestDetail } from '@/hooks/useRequests';
import type { RequestAction, RequestDetail } from '@/types/request';

export const PENDING_MODAL: Record<
  ActionKind,
  { title: string; label: string; required: boolean; danger: boolean; done: string }
> = {
  approve: {
    title: MESSAGES.requests.approve,
    label: MESSAGES.requests.approveNote,
    required: false,
    danger: false,
    done: MESSAGES.requests.approved,
  },
  reject: {
    title: MESSAGES.requests.reject,
    label: MESSAGES.requests.rejectReason,
    required: true,
    danger: true,
    done: MESSAGES.requests.rejected,
  },
  complete: {
    title: MESSAGES.requests.complete,
    label: MESSAGES.requests.resultNote,
    required: false,
    danger: false,
    done: MESSAGES.requests.completed,
  },
  notComplete: {
    title: MESSAGES.requests.notComplete,
    label: MESSAGES.requests.notCompleteReason,
    required: true,
    danger: true,
    done: MESSAGES.requests.notCompleted,
  },
  cancel: {
    title: MESSAGES.requests.cancelRequest,
    label: MESSAGES.requests.cancelReason,
    required: false,
    danger: true,
    done: MESSAGES.requests.cancelled,
  },
};

/** Trạng thái và xử lý của RequestDetailModal; component chỉ lo giao diện. */
export function useRequestDetailModal() {
  const [searchParams, setSearchParams] = useSearchParams();
  const idParam = Number(searchParams.get('id'));
  const id = Number.isInteger(idParam) && idParam > 0 ? idParam : null;
  const { data, isLoading, error, refetch } = useRequestDetail(id);
  const act = useRequestAction();
  const [pending, setPending] = useState<ActionKind | null>(null);
  const [editing, setEditing] = useState(false);

  const close = () => setSearchParams({});
  const notFound = (error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound;

  const submit = (note: string | null) => {
    if (!pending || !data) return;
    const meta = PENDING_MODAL[pending];
    act.mutate(buildAction(pending, data, note), {
      onSuccess: () => void message.success(meta.done),
      onSettled: () => setPending(null),
    });
  };

  return {
    id,
    act,
    close,
    notFound,
    submit,
    pending,
    setPending,
    editing,
    setEditing,
    data,
    isLoading,
    error,
    refetch,
  };
}

type ActionKind = RequestAction['kind'];

const buildAction = (
  kind: ActionKind,
  detail: RequestDetail,
  note: string | null,
): RequestAction => {
  const base = { id: detail.requestId, rowVer: detail.rowVer };
  switch (kind) {
    case 'approve':
      return { kind, ...base, note };
    case 'reject':
      return { kind, ...base, reason: note ?? '' };
    case 'complete':
      return { kind, ...base, resultNote: note };
    case 'notComplete':
      return { kind, ...base, reason: note ?? '' };
    case 'cancel':
      return { kind, ...base, reason: note };
  }
};
