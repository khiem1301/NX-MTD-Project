import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { message } from 'antd';
import type { ApiError } from '@/types/api';

type Options = {
  /** Các query key bị ảnh hưởng, invalidate sau khi thành công. */
  invalidate: QueryKey[];
  successMessage?: string;
};

/** Mutation chuẩn: thành công → invalidate + thông báo; lỗi → hiện message theo errorCode BE trả. */
export const useApiMutation = <TVars, TData = unknown>(
  mutationFn: (vars: TVars) => Promise<TData>,
  { invalidate, successMessage }: Options,
) => {
  const queryClient = useQueryClient();
  return useMutation<TData, ApiError, TVars>({
    mutationFn,
    onSuccess: async () => {
      await Promise.all(invalidate.map((queryKey) => queryClient.invalidateQueries({ queryKey })));
      if (successMessage) void message.success(successMessage);
    },
    onError: (error) => {
      void message.error(error.message);
    },
  });
};
