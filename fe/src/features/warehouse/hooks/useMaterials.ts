import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
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
  const role = useRolePath();
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
