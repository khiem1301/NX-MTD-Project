import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

/** Id của đối tượng đang xem chi tiết, lưu ở query `?id=` để link / nút Back vẫn mở đúng Modal. */
export function useQueryId(): [number | null, () => void] {
  const [params, setParams] = useSearchParams();
  const value = Number(params.get('id'));
  const id = Number.isInteger(value) && value > 0 ? value : null;
  const close = useCallback(() => {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete('id');
        return next;
      },
      { replace: true },
    );
  }, [setParams]);
  return [id, close];
}

/**
 * Phiếu đang xem chi tiết: chọn từ danh sách hoặc mở sẵn từ link `?id=` (VD bấm thông báo số nhận thấp hơn số gửi, DEV-ALL-196).
 * `close` đóng Modal và bỏ `?id=` khỏi địa chỉ.
 */
export function useViewingId(): [number | undefined, (id: number) => void, () => void] {
  const [queryId, closeQuery] = useQueryId();
  const [picked, setPicked] = useState<number>();
  const close = useCallback(() => {
    setPicked(undefined);
    closeQuery();
  }, [closeQuery]);
  return [picked ?? queryId ?? undefined, setPicked, close];
}
