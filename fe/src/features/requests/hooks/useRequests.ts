import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import type { ApiError } from '@/types/api';
import { requestApi } from '../api/requestApi';
import type {
  CreateRequestBody,
  RequestAction,
  RequestListQuery,
  UpdateRequestBody,
} from '../types';

export const REQUESTS_KEY = ['requests'] as const;

export const useRequestTypes = () =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'types'],
    queryFn: requestApi.types,
    staleTime: Infinity,
  });

export const useMyRequests = (query: RequestListQuery) =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'mine', query],
    queryFn: () => requestApi.mine(query),
    placeholderData: keepPreviousData,
  });

export const useRequestInbox = (query: RequestListQuery) =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'inbox', query],
    queryFn: () => requestApi.inbox(query),
    placeholderData: keepPreviousData,
  });

export const useRequestDetail = (id: number | null) =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'detail', id],
    queryFn: () => requestApi.detail(id as number),
    enabled: id !== null,
  });

/** Danh sách + chi tiết đều nằm dưới key 'requests' → một lần invalidate là đủ. */
export const useCreateRequest = () =>
  useApiMutation((body: CreateRequestBody) => requestApi.create(body), {
    invalidate: [REQUESTS_KEY],
  });

export const useUpdateRequest = (id: number) =>
  useApiMutation((body: UpdateRequestBody) => requestApi.update(id, body), {
    invalidate: [REQUESTS_KEY],
    successMessage: MESSAGES.common.saved,
  });

/** Lỗi 409 (đơn đã đổi, đã xử lý...) cũng tải lại để người dùng làm lại trên dữ liệu mới. */
export const useRequestAction = () => {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: REQUESTS_KEY });
  return useMutation<void, ApiError, RequestAction>({
    mutationFn: (action) => requestApi.act(action),
    onSuccess: refresh,
    onError: (error) => {
      void refresh();
      void message.error(
        error.errorCode === ERROR_CODES.CommonConcurrencyConflict
          ? MESSAGES.common.concurrency
          : error.message,
      );
    },
  });
};
