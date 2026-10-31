import { useCallback } from 'react';
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
