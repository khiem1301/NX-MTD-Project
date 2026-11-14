import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { requestApi } from '@/services/requestApi';
import { useAuthStore } from '@/store/authStore';
import type { TeamType, UserInfo } from '@/types/auth';
import { workOrderRequisitionApi } from '../api/workOrderRequisitionApi';
import { RequestFormModal } from './RequestFormModal';

jest.mock('@/services/requestApi', () => ({
  requestApi: { create: jest.fn(), update: jest.fn(), types: jest.fn() },
}));
jest.mock('../api/workOrderRequisitionApi', () => ({
  workOrderRequisitionApi: { teamWorkOrders: jest.fn(), allowance: jest.fn() },
}));
jest.mock('@/hooks/useAttachments', () => ({ attachmentApi: { upload: jest.fn() } }));
jest.mock('@/services/env', () => ({ env: { apiBaseUrl: 'http://localhost' } }));
jest.mock('@/services/lookupApi', () => ({
  lookupApi: {
    orders: jest.fn().mockResolvedValue([
      {
        orderId: 4,
        orderCode: 'DH4',
        status: 'InProduction',
        materialStatus: 'Complete',
        dueDate: '2026-12-01',
        receivedDate: '2026-10-01',
      },
    ]),
  },
}));
jest.mock('../api/requisitionLookupApi', () => ({
  requisitionLookupApi: {
    materials: jest.fn().mockResolvedValue([
      {
        materialId: 1,
        materialCode: 'V1',
        materialName: 'Kate',
        color: '',
        unit: 'm',
        materialType: 'FABRIC',
        availableQty: 1000,
      },
      {
        materialId: 2,
        materialCode: 'C1',
        materialName: 'Chỉ',
        color: '',
        unit: 'cuộn',
        materialType: 'TRIM',
        availableQty: 50,
      },
    ]),
    pieces: jest.fn().mockResolvedValue([
      {
        orderLineId: 11,
        productCode: 'AO1',
        productName: 'Áo',
        color: 'Đỏ',
        sizeCode: 'M',
        orderedQty: 100,
        availableQty: 30,
      },
    ]),
  },
}));

const signIn = (teamType: TeamType) =>
  useAuthStore.setState({
    status: 'authenticated',
    user: {
      accountId: 1,
      employeeId: 1,
      username: 'U',
      fullName: 'User',
      role: 'TeamLeader',
      roleName: 'TeamLeader',
      teamId: 2,
      teamName: 'Tổ cắt',
      teamType,
      isTemporaryPassword: false,
    } satisfies UserInfo,
  });

const renderModal = () => {
  (requestApi.types as jest.Mock).mockResolvedValue([
    {
      requestType: 'MaterialRequisition',
      label: 'Xin NVL',
      needsApproval: true,
      handlerRole: 'WarehouseKeeper',
      handlerRoleOptions: null,
    },
    {
      requestType: 'SemiFinishedRequisition',
      label: 'Xin BTP',
      needsApproval: true,
      handlerRole: 'WarehouseKeeper',
      handlerRoleOptions: null,
    },
  ]);
  (workOrderRequisitionApi.teamWorkOrders as jest.Mock).mockResolvedValue([
    {
      workOrderId: 7,
      workOrderCode: 'LC-7',
      orderId: 4,
      orderCode: 'DH4',
      productCode: 'AO1',
      dueDate: '2026-12-01',
      status: 'OPEN',
    },
  ]);
  (workOrderRequisitionApi.allowance as jest.Mock).mockResolvedValue([
    {
      materialId: 1,
      materialCode: 'V1',
      materialName: 'Kate',
      materialColor: '',
      unit: 'm',
      needQty: 100,
      capQty: 105,
      requestedQty: 30,
      remainingQty: 75,
    },
  ]);
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <RequestFormModal open maxPhotos={5} onClose={jest.fn()} onSaved={jest.fn()} />
    </QueryClientProvider>,
  );
};

const chooseOption = async (selectIndex: number, title: string) => {
  await waitFor(() =>
    expect(document.querySelectorAll('.ant-select-selector').length).toBeGreaterThan(selectIndex),
  );
  fireEvent.mouseDown(document.querySelectorAll('.ant-select-selector')[selectIndex] as Element);
  await waitFor(() =>
    expect(document.querySelector(`.ant-select-item-option[title^="${title}"]`)).not.toBeNull(),
  );
  fireEvent.click(document.querySelector(`.ant-select-item-option[title^="${title}"]`) as Element);
};

describe('Đơn xin NVL theo lệnh cắt', () => {
  it('tổ may không có ô chọn lệnh cắt', async () => {
    signIn('Sewing');
    renderModal();
    await chooseOption(0, 'Xin NVL');

    expect(screen.queryByText(MESSAGES.requests.workOrder)).not.toBeInTheDocument();
  });

  const pickWorkOrder = async () => {
    signIn('Cutting');
    renderModal();
    await chooseOption(0, 'Xin NVL');
    await screen.findByText(MESSAGES.requests.workOrder);
    await chooseOption(1, 'LC-7');
    await waitFor(() =>
      expect(screen.getAllByText(MESSAGES.requests.allowanceRemaining).length).toBeGreaterThan(0),
    );
    fireEvent.change(screen.getByPlaceholderText(MESSAGES.requests.contentPlaceholder), {
      target: { value: 'Xin vải cho lệnh' },
    });
  };

  beforeEach(() => (requestApi.create as jest.Mock).mockReset().mockResolvedValue(9));

  it('tổ cắt chọn lệnh: tự điền vải, số còn thiếu so với số hệ thống tính, gửi kèm lệnh', async () => {
    await pickWorkOrder();

    fireEvent.click(screen.getByText(MESSAGES.common.save));

    await waitFor(() => expect(requestApi.create).toHaveBeenCalled());
    expect((requestApi.create as jest.Mock).mock.calls[0][0]).toMatchObject({
      requestType: 'MaterialRequisition',
      workOrderId: 7,
      orderId: 4,
      lines: [{ materialId: 1, quantity: 70 }],
    });
  });

  it('nhập vượt số còn xin được của lệnh thì báo lỗi, không gửi', async () => {
    await pickWorkOrder();

    fireEvent.change(screen.getByLabelText(MESSAGES.requests.quantity), {
      target: { value: '80' },
    });
    fireEvent.click(screen.getByText(MESSAGES.common.save));

    expect(
      await screen.findByText(MESSAGES.requests.overRemaining.replace('{0}', '75')),
    ).toBeInTheDocument();
    expect(requestApi.create).not.toHaveBeenCalled();
  });
});

describe('Đơn xin NVL / BTP của tổ may (DEV-ALL-110)', () => {
  beforeEach(() => (requestApi.create as jest.Mock).mockReset().mockResolvedValue(9));

  const start = async (type: string) => {
    signIn('Sewing');
    renderModal();
    await chooseOption(0, type);
    fireEvent.change(screen.getByPlaceholderText(MESSAGES.requests.contentPlaceholder), {
      target: { value: 'Cần hàng' },
    });
  };

  it('chưa chọn đơn hàng thì ô chọn hàng bị khóa và không gửi được', async () => {
    await start('Xin BTP');

    expect(await screen.findByText(MESSAGES.requests.orderFirst)).toBeInTheDocument();
    fireEvent.click(screen.getByText(MESSAGES.common.save));

    await waitFor(() =>
      expect(screen.getAllByText(MESSAGES.common.required).length).toBeGreaterThan(0),
    );
    expect(requestApi.create).not.toHaveBeenCalled();
  });

  it('xin BTP: chọn đơn và mã hàng, gửi kèm đơn hàng và dòng đơn', async () => {
    await start('Xin BTP');
    await chooseOption(1, 'DH4');
    await chooseOption(2, 'AO1 Đỏ/M');
    fireEvent.change(screen.getByLabelText(MESSAGES.requests.quantity), {
      target: { value: '30' },
    });
    fireEvent.click(screen.getByText(MESSAGES.common.save));

    await waitFor(() => expect(requestApi.create).toHaveBeenCalled());
    expect((requestApi.create as jest.Mock).mock.calls[0][0]).toMatchObject({
      requestType: 'SemiFinishedRequisition',
      orderId: 4,
      lines: [{ orderLineId: 11, materialId: null, itemDescription: null, quantity: 30 }],
    });
  });

  it('xin BTP vượt số khả dụng trong kho thì báo lỗi, không gửi', async () => {
    await start('Xin BTP');
    await chooseOption(1, 'DH4');
    await chooseOption(2, 'AO1 Đỏ/M');
    fireEvent.change(screen.getByLabelText(MESSAGES.requests.quantity), {
      target: { value: '31' },
    });
    fireEvent.click(screen.getByText(MESSAGES.common.save));

    expect(
      await screen.findByText(MESSAGES.requests.overStock.replace('{0}', '30')),
    ).toBeInTheDocument();
    expect(requestApi.create).not.toHaveBeenCalled();
  });

  it('xin NVL: chọn đơn rồi chọn NVL của đơn, gửi kèm đơn hàng', async () => {
    await start('Xin NVL');
    await chooseOption(1, 'DH4');
    await chooseOption(2, 'C1 Chỉ');
    fireEvent.change(screen.getByLabelText(MESSAGES.requests.quantity), {
      target: { value: '5' },
    });
    fireEvent.click(screen.getByText(MESSAGES.common.save));

    await waitFor(() => expect(requestApi.create).toHaveBeenCalled());
    expect((requestApi.create as jest.Mock).mock.calls[0][0]).toMatchObject({
      requestType: 'MaterialRequisition',
      orderId: 4,
      workOrderId: null,
      lines: [{ materialId: 2, quantity: 5 }],
    });
  });
});
