import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { useApiMutation } from '@/hooks/useApiMutation';
import { vehicleLogApi } from '../api/vehicleLogApi';
import { VL_MESSAGES } from '../constants';
import type {
  CompleteTripPayload,
  SaveVehiclePayload,
  StartTripPayload,
  TripQuery,
  VehicleQuery,
} from '../types';

export const VEHICLE_KEY = ['vehicle-log'] as const;
const AFFECTED = [VEHICLE_KEY];

export const useVehicles = (query: VehicleQuery) =>
  useQuery({
    queryKey: [...VEHICLE_KEY, 'vehicles', query],
    queryFn: () => vehicleLogApi.vehicles(query),
    placeholderData: keepPreviousData,
  });

export const useTrips = (query: TripQuery) =>
  useQuery({
    queryKey: [...VEHICLE_KEY, 'trips', query],
    queryFn: () => vehicleLogApi.trips(query),
    placeholderData: keepPreviousData,
  });

export const useSaveVehicle = () =>
  useApiMutation(
    async ({ id, body }: { id?: number; body: SaveVehiclePayload }) =>
      id
        ? (await vehicleLogApi.updateVehicle(id, body)).data
        : (await vehicleLogApi.createVehicle(body)).data,
    { invalidate: AFFECTED, successMessage: VL_MESSAGES.vehicleSaved },
  );

export const useChangeVehicleStatus = () =>
  useApiMutation(
    async ({ id, active }: { id: number; active: boolean }) =>
      active ? vehicleLogApi.reactivate(id) : vehicleLogApi.discontinue(id),
    { invalidate: AFFECTED, successMessage: VL_MESSAGES.vehicleSaved },
  );

/** Bắt đầu chuyến: km đầu không liền chuyến trước vẫn lưu nhưng hiện cảnh báo MSG30 do BE trả về. */
export const useStartTrip = () =>
  useApiMutation(
    async (body: StartTripPayload) => {
      const response = await vehicleLogApi.startTrip(body);
      if (response.data.kmGapWarning) void message.warning(response.message, 8);
      else void message.success(response.message ?? VL_MESSAGES.tripStarted);
      return response.data;
    },
    { invalidate: AFFECTED },
  );

/** Kết thúc chuyến: xăng vượt định mức quá dung sai vẫn lưu nhưng hiện cảnh báo MSG30. */
export const useCompleteTrip = () =>
  useApiMutation(
    async ({ id, body }: { id: number; body: CompleteTripPayload }) => {
      const response = await vehicleLogApi.completeTrip(id, body);
      if (response.data.fuelOverNormWarning) void message.warning(response.message, 8);
      else void message.success(response.message ?? VL_MESSAGES.tripCompleted);
      return response.data;
    },
    { invalidate: AFFECTED },
  );
