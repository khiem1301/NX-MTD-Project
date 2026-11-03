import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { MaterialQuery, SaveMaterialPayload } from '../types';

export const WAREHOUSE_KEY = ['warehouse'] as const;
const LOOKUP_MATERIALS_KEY = ['lookups', 'materials'] as const;

export const useMaterials = (query: MaterialQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'materials', role, query],
    queryFn: () => warehouseApi.getMaterials(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useSaveMaterial = () => {
  const rolePath = useRolePath();
  // Kế toán chỉ thêm NVL mới từ màn định mức mã hàng, không dùng các API kho khác theo role.
  const role = usePermission().hasRole(ROLES.Accountant) ? 'accountant' : rolePath;
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveMaterialPayload }) =>
      id ? warehouseApi.updateMaterial(role, id, body) : warehouseApi.createMaterial(role, body),
    {
      invalidate: [WAREHOUSE_KEY, LOOKUP_MATERIALS_KEY],
      successMessage: MESSAGES.warehouse.materialSaved,
    },
  );
};

export const useChangeMaterialStatus = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, status }: { id: number; status: string }) =>
      warehouseApi.changeMaterialStatus(role, id, status),
    {
      invalidate: [WAREHOUSE_KEY, LOOKUP_MATERIALS_KEY],
      successMessage: MESSAGES.warehouse.materialStatusChanged,
    },
  );
};
