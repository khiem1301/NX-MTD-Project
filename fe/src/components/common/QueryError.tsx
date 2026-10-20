import { Alert, Button } from 'antd';
import { MESSAGES } from '@/constants/messages';

/** apiClient luôn reject bằng ApiError; TanStack Query gõ kiểu Error nên chỉ cần message. */
type QueryErrorProps = { error: { message: string } | null; onRetry: () => void };

/** Thông báo lỗi khi tải dữ liệu thất bại, kèm nút thử lại. */
export function QueryError({ error, onRetry }: QueryErrorProps) {
  if (!error) return null;
  return (
    <Alert
      type="error"
      showIcon
      message={error.message}
      style={{ marginBottom: 16 }}
      action={<Button onClick={onRetry}>{MESSAGES.common.retry}</Button>}
    />
  );
}
