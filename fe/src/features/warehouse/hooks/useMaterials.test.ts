import { renderHook } from '@testing-library/react';
import { ROLES, type Role } from '@/constants/roles';
import { warehouseApi } from '../api/warehouseApi';
import { useSaveMaterial } from './useMaterials';

let mockRole: Role = ROLES.Accountant;
jest.mock('@/hooks/usePermission', () => ({
  usePermission: () => ({ hasRole: (role: Role) => role === mockRole }),
}));
jest.mock('@/hooks/useRolePath', () => ({ useRolePath: () => 'manager' }));
jest.mock('@/hooks/useApiMutation', () => ({ useApiMutation: (fn: unknown) => ({ fn }) }));
jest.mock('../api/warehouseApi', () => ({ warehouseApi: { createMaterial: jest.fn() } }));

type Save = (arg: { body: unknown }) => void;
const callSave = () => {
  const { result } = renderHook(() => useSaveMaterial());
  (result.current as unknown as { fn: Save }).fn({ body: {} });
};

describe('useSaveMaterial', () => {
  beforeEach(() => jest.mocked(warehouseApi.createMaterial).mockClear());

  it('Kế toán tạo NVL qua API của Kế toán', () => {
    mockRole = ROLES.Accountant;
    callSave();

    expect(warehouseApi.createMaterial).toHaveBeenCalledWith('accountant', {});
  });

  it('role khác giữ API theo role', () => {
    mockRole = ROLES.Manager;
    callSave();

    expect(warehouseApi.createMaterial).toHaveBeenCalledWith('manager', {});
  });
});
