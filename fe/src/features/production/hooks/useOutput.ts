import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { outputKey } from './productionKeys';
import type { OutputGridQuery, OutputRole, SaveOutputPayload } from '../types';

// ---------- Sản lượng ----------

export const useOutputGrid = (role: OutputRole, query: OutputGridQuery | null) =>
  useQuery({
    queryKey: [...outputKey, role, query],
    queryFn: () => productionApi.getOutputGrid(role, query!),
    enabled: query !== null,
  });

export const useSaveOutput = (role: OutputRole) =>
  useApiMutation((body: SaveOutputPayload) => productionApi.saveOutput(role, body), {
    invalidate: [outputKey],
  });
