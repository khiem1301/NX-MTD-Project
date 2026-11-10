import { useQuery } from '@tanstack/react-query';
import { rjEmployeeApi } from '@/services/rjEmployeeApi';

/** Nằm dưới key 'attendance' để đồng bộ / gán mã chấm công xong tự làm mới. */
export const useRjEmployeesCommon = (onlyUnmapped: boolean, enabled = true) =>
  useQuery({
    queryKey: ['attendance', 'rj-employees-common', onlyUnmapped] as const,
    queryFn: () => rjEmployeeApi.listCommon(onlyUnmapped),
    enabled,
  });
