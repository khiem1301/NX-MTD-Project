import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { vehicleStatusApi } from '../api/vehicleStatusApi';
import type { VehicleLiveStatusQuery } from '../types';

export const VEHICLE_STATUS_KEY = ['vehicle-status'] as const;

/** Trạng thái chạy thay đổi theo chuyến của Kế toán / Thủ kho: tự làm mới mỗi 30 giây. */
export const useVehicleStatuses = (query: VehicleLiveStatusQuery) =>
  useQuery({
    queryKey: [...VEHICLE_STATUS_KEY, query],
    queryFn: () => vehicleStatusApi.list(query),
    placeholderData: keepPreviousData,
    refetchInterval: 30_000,
  });
