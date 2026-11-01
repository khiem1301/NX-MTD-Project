const fit = (px: number): string => `min(${px}px, calc(100vw - 16px))`;

/** Bốn cỡ Modal chuẩn (tự co theo màn hình hẹp): sm cho form ngắn, md cho form có bảng nhỏ, lg cho form nhiều cột / nhiều dòng, xl cho bảng nhập liệu rất rộng (VD định mức). */
export const MODAL_WIDTH = { sm: fit(520), md: fit(760), lg: fit(960), xl: fit(1240) } as const;
