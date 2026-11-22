import type { PageParams } from '@/types/api';

export type VehicleLiveStatus = {
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  status: string;
  isRunning: boolean;
  openTripCount: number;
  currentTripId: number | null;
  tripDate: string | null;
  driverName: string | null;
  dispatchCode: string | null;
  odometerStart: number | null;
  lastOdometer: number | null;
};

export type VehicleLiveStatusQuery = PageParams & { running?: boolean };
