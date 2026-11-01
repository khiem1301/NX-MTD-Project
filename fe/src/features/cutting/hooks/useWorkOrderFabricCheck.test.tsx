import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { cuttingApi } from '../api/cuttingApi';
import type { CheckWorkOrderFabricPayload, WorkOrderFabricCheck } from '../types';
import { useWorkOrderFabricCheck } from './useCutting';

jest.mock('@/hooks/useRolePath', () => ({ useManagerRolePath: () => 'manager' }));
jest.mock('../api/cuttingApi', () => ({ cuttingApi: { checkWorkOrderFabric: jest.fn() } }));

const check = (materialId: number): WorkOrderFabricCheck => ({
  materialId,
  materialCode: 'V01',
  materialName: 'Vải',
  materialColor: '',
  status: 'Short',
  needMeters: 100,
  availableQty: 50,
  message: 'Thiếu vải',
});

const body = (orderId: number): CheckWorkOrderFabricPayload => ({
  orderId,
  cuttingTeamId: 1,
  lines: [{ orderLineId: 1, plannedQty: 10, productComponentMaterialId: null }],
});

const setup = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return renderHook(({ payload }) => useWorkOrderFabricCheck(payload), {
    wrapper,
    initialProps: { payload: body(1) as CheckWorkOrderFabricPayload | null },
  });
};

describe('useWorkOrderFabricCheck', () => {
  beforeEach(() => {
    jest.mocked(cuttingApi.checkWorkOrderFabric).mockReset();
  });

  it('form không còn đủ thông tin thì bỏ cảnh báo ngay, không chờ', async () => {
    jest.mocked(cuttingApi.checkWorkOrderFabric).mockResolvedValue([check(1)]);
    const { result, rerender } = setup();
    await waitFor(() => expect(result.current).toHaveLength(1));

    rerender({ payload: null });

    expect(result.current).toHaveLength(0);
  });

  it('đổi sang đơn khác thì không giữ cảnh báo của đơn cũ', async () => {
    jest
      .mocked(cuttingApi.checkWorkOrderFabric)
      .mockResolvedValueOnce([check(1)])
      .mockImplementation(() => new Promise(() => undefined));
    const { result, rerender } = setup();
    await waitFor(() => expect(result.current).toHaveLength(1));

    rerender({ payload: body(2) });

    await waitFor(() => expect(cuttingApi.checkWorkOrderFabric).toHaveBeenCalledTimes(2));
    expect(result.current).toHaveLength(0);
  });
});
