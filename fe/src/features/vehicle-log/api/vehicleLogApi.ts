import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CompleteTripPayload,
  SaveVehiclePayload,
  StartTripPayload,
  TripQuery,
  Vehicle,
  VehicleQuery,
  VehicleTrip,
} from '../types';

// Trả cả ApiResponse vì message có thể là cảnh báo MSG30 (lệch km / xăng vượt định mức).
const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<ApiResponse<T>> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data;
const put = async <T>(url: string, body: unknown): Promise<ApiResponse<T>> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data;

const VEHICLES = '/api/v1/accountant/vehicles';
const TRIPS = '/api/v1/accountant/vehicle-trips';

export const vehicleLogApi = {
  vehicles: (q: VehicleQuery) => get<Paginated<Vehicle>>(VEHICLES, q),
  createVehicle: (body: SaveVehiclePayload) => post<Vehicle>(VEHICLES, body),
  updateVehicle: (id: number, body: SaveVehiclePayload) => put<Vehicle>(`${VEHICLES}/${id}`, body),
  discontinue: (id: number) => post<null>(`${VEHICLES}/${id}/discontinue`),
  reactivate: (id: number) => post<null>(`${VEHICLES}/${id}/reactivate`),

  trips: (q: TripQuery) => get<Paginated<VehicleTrip>>(TRIPS, q),
  startTrip: (body: StartTripPayload) => post<VehicleTrip>(TRIPS, body),
  completeTrip: (id: number, body: CompleteTripPayload) =>
    post<VehicleTrip>(`${TRIPS}/${id}/complete`, body),
};
