import { useEffect } from 'react';
import { useMediaQuery } from './useMediaQuery';

/** Đặt trên body để CSS áp được cả cho hộp thoại, popup (render ngoài layout). */
const WORKSHOP_BODY_CLASS = 'workshop-ui';

/**
 * Bật giao diện xưởng cho người dùng thuộc vai trò xưởng. Cỡ nút, bảng, hộp thoại chỉ đổi trên màn hẹp
 * (media query trong CSS). Trả về true khi đang ở màn điện thoại của vai trò xưởng.
 */
export function useWorkshopMobile(enabled: boolean, query: string): boolean {
  const isNarrow = useMediaQuery(query);
  useEffect(() => {
    if (!enabled) return undefined;
    document.body.classList.add(WORKSHOP_BODY_CLASS);
    return () => document.body.classList.remove(WORKSHOP_BODY_CLASS);
  }, [enabled]);
  return enabled && isNarrow;
}
