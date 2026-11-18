import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { SaveMaterialPayload } from '@/types/material';

/**
 * Danh mục NVL dùng ngoài feature kho (POST /api/v1/<role>/materials). DEV-ALL-123: popup kiểm tra Excel định mức của mã hàng
 * thêm NVL chưa có vào danh mục khi lưu. Feature kho có hàm riêng trả về đủ thông tin NVL cho màn danh mục.
 */
export const materialApi = {
  create: async (role: RolePath, body: SaveMaterialPayload): Promise<void> => {
    await apiClient.post<ApiResponse<unknown>>(API.warehouse.materials(role), body);
  },
};
