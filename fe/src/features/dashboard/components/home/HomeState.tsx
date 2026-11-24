import type { ReactNode } from 'react';
import { Spin } from 'antd';
import type { UseQueryResult } from '@tanstack/react-query';
import { QueryError } from '@/components/common/QueryError';

/** Chờ / lỗi của một lần gọi API trang chủ; có dữ liệu thì vẽ phần thân. */
export function HomeState<T>({
  query,
  children,
}: {
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return <Spin />;
  if (query.isError) return <QueryError error={query.error} onRetry={() => void query.refetch()} />;
  return <>{children(query.data)}</>;
}
