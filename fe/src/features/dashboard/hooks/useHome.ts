import { useQuery } from '@tanstack/react-query';
import { homeApi } from '../api/homeApi';

const HOME_KEY = ['dashboard', 'home'] as const;

export const useAdminHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'admin'], queryFn: homeApi.admin });

export const useManagerHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'manager'], queryFn: homeApi.manager });

export const useAccountantHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'accountant'], queryFn: homeApi.accountant });

export const useWarehouseHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'warehouse'], queryFn: homeApi.warehouse });

export const useTeamLeaderHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'team-leader'], queryFn: homeApi.teamLeader });

export const useQaqcHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'qaqc'], queryFn: homeApi.qaqc });

export const useWorkerHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'worker'], queryFn: homeApi.worker });

export const useElectricianHome = () =>
  useQuery({ queryKey: [...HOME_KEY, 'electrician'], queryFn: homeApi.electrician });
