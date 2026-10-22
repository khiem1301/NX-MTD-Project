import type { PageParams } from '@/types/api';

export type Vehicle = {
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  fuelNormPer100Km: number;
  status: string;
  tripCount: number;
  lastOdometer: number | null;
};

export type VehicleTrip = {
  vehicleTripId: number;
  vehicleId: number;
  plateNumber: string;
  tripDate: string;
  driverName: string;
  driverEmployeeId: number | null;
  purpose: string | null;
  dispatchNoteId: number | null;
  dispatchCode: string | null;
  odometerStart: number;
  odometerEnd: number | null;
  distance: number | null;
  fuelLiters: number | null;
  fuelCost: number | null;
  fuelNormLiters: number | null;
  status: string;
  kmGapWarning: boolean;
  fuelOverNormWarning: boolean;
  recordedAt: string;
};

export type VehicleQuery = PageParams & { status?: string };

export type TripQuery = PageParams & {
  vehicleId?: number;
  status?: string;
  fromDate?: string;
  toDate?: string;
  warningsOnly?: boolean;
};

export type SaveVehiclePayload = {
  plateNumber: string;
  vehicleType: string;
  fuelNormPer100Km: number;
};

export type StartTripPayload = {
  vehicleId: number;
  tripDate: string;
  driverName: string;
  purpose?: string;
  odometerStart: number;
};

export type CompleteTripPayload = {
  odometerEnd: number;
  fuelLiters?: number | null;
  fuelCost?: number | null;
};
