import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { FROM_REQUEST_PARAM, FROM_REQUEST_TYPE_PARAM } from '@/constants/routes';

/**
 * Màn xử lý đơn từ mở bằng link "Xử lý ngay" (`?fromRequest=<id>&requestType=<loại>`).
 * Đọc đơn cần xử lý một lần lúc vào màn rồi bỏ hai tham số khỏi URL, để tải lại trang không mở lại form.
 */
export const useFromRequest = (): { requestId: number | null; requestType: string | null } => {
  const [searchParams, setSearchParams] = useSearchParams();
  const [preset] = useState(() => {
    const id = Number(searchParams.get(FROM_REQUEST_PARAM));
    return {
      requestId: Number.isInteger(id) && id > 0 ? id : null,
      requestType: searchParams.get(FROM_REQUEST_TYPE_PARAM),
    };
  });
  const cleared = useRef(false);

  useEffect(() => {
    if (preset.requestId === null || cleared.current) return;
    cleared.current = true;
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete(FROM_REQUEST_PARAM);
        next.delete(FROM_REQUEST_TYPE_PARAM);
        return next;
      },
      { replace: true },
    );
  }, [preset.requestId, setSearchParams]);

  return preset;
};
