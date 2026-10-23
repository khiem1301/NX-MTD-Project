import { useQuery } from '@tanstack/react-query';
import { kpiBtpApi } from '../api/kpiBtpApi';
import { useRolePath } from '@/hooks/useRolePath';

const KEY = ['kpi', 'btp-compare'] as const;

/** Danh sách KPI chọn được; tổ trưởng / tổ phó chỉ thấy KPI của tổ mình (BE lọc theo token). */
export const useKpiBtpOptions = () => {
  const role = useRolePath();
  return useQuery({ queryKey: [...KEY, role, 'options'], queryFn: () => kpiBtpApi.options(role) });
};

export const useKpiBtpComparison = (kpiTargetId: number | null) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...KEY, role, kpiTargetId],
    queryFn: () => kpiBtpApi.comparison(role, kpiTargetId ?? 0),
    enabled: kpiTargetId !== null,
  });
};
