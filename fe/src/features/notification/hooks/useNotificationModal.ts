import { create } from 'zustand';

/** Trạng thái UI toàn cục: cửa sổ chi tiết đang mở từ một thông báo (không chuyển trang). Chỉ chứa id, không chứa dữ liệu API. */
type NotificationModalState = {
  mismatchTargetId: number | null;
  openMismatch: (kpiTargetId: number) => void;
  closeMismatch: () => void;
};

export const useNotificationModal = create<NotificationModalState>((set) => ({
  mismatchTargetId: null,
  openMismatch: (kpiTargetId) => set({ mismatchTargetId: kpiTargetId }),
  closeMismatch: () => set({ mismatchTargetId: null }),
}));
