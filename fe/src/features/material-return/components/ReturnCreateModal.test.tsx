import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { useAuthStore } from '@/store/authStore';
import type { TeamType, UserInfo } from '@/types/auth';
import { RETURN_LABELS } from '../constants';
import type { CreateReturnPayload, ReturnableLine, ReturnableQuery } from '../types';
import { ReturnCreateModal } from './ReturnCreateModal';

const material = (id: number, usedQty: number | null, remainingQty = 100): ReturnableLine => ({
  materialId: id,
  orderLineId: null,
  itemCode: `V0${id}`,
  itemName: 'Vải gió',
  unit: 'm',
  itemColor: 'Đen',
  receivedQty: 100,
  usedQty,
  returnedQty: 0,
  remainingQty,
});

const piece: ReturnableLine = {
  materialId: null,
  orderLineId: 70,
  itemCode: 'MH3',
  itemName: 'Đen/M',
  unit: 'bộ',
  itemColor: 'Đen',
  receivedQty: 100,
  usedQty: 40,
  returnedQty: 0,
  remainingQty: 60,
};

let mockLines: ReturnableLine[] = [];
const mockCreate = jest.fn();
const mockReturnable = jest.fn();
jest.mock('../hooks/useMaterialReturn', () => ({
  useReturnable: (query: ReturnableQuery | undefined) => {
    mockReturnable(query);
    return { data: query ? mockLines : undefined, isLoading: false };
  },
  useReturnWorkOrders: () => ({
    data: [
      {
        workOrderId: 7,
        workOrderCode: 'LC-7',
        orderId: 4,
        orderCode: 'DH4',
        productCode: 'AO1',
        dueDate: '2026-12-01',
        status: 'OPEN',
      },
    ],
    isLoading: false,
  }),
  useCreateReturn: () => ({ mutate: mockCreate, isPending: false }),
}));
jest.mock('@/hooks/useLookups', () => ({
  useOrderOptions: () => ({
    data: [
      {
        orderId: 4,
        orderCode: 'DH4',
        status: 'InProduction',
        materialStatus: 'Complete',
        dueDate: '2026-12-01',
        receivedDate: '2026-10-01',
      },
    ],
    isLoading: false,
  }),
  useScrapMaterialOptions: () => ({
    data: [
      {
        materialId: 9,
        materialCode: 'PP-0001',
        materialName: 'Vải vụn',
        materialType: 'SCRAP',
        unit: 'kg',
        color: '',
      },
    ],
    isLoading: false,
  }),
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
      teamName: 'Tổ',
      teamType,
      isTemporaryPassword: false,
    } satisfies UserInfo,
  });

/** Mở ô chọn thứ comboIndex rồi chọn mục có title bắt đầu bằng titlePrefix. */
const choose = async (comboIndex: number, titlePrefix: string) => {
  fireEvent.mouseDown(screen.getAllByRole('combobox')[comboIndex] as HTMLElement);
  await waitFor(() =>
    expect(
      document.querySelector(`.ant-select-item-option[title^="${titlePrefix}"]`),
    ).not.toBeNull(),
  );
  fireEvent.click(
    document.querySelector(`.ant-select-item-option[title^="${titlePrefix}"]`) as Element,
  );
};

const open = () => render(<ReturnCreateModal open onClose={jest.fn()} />);

beforeEach(() => {
  mockCreate.mockReset();
  mockReturnable.mockReset();
  mockLines = [];
});

describe('ReturnCreateModal – tổ cắt', () => {
  beforeEach(() => signIn('Cutting'));

  it('chưa chọn lệnh cắt thì chưa tải vải và nhắc chọn lệnh', () => {
    open();

    expect(screen.getByText(RETURN_LABELS.pickWorkOrder)).toBeInTheDocument();
    expect(mockReturnable).toHaveBeenLastCalledWith(undefined);
  });

  it('chọn lệnh cắt thì tải vải của lệnh và điền sẵn số còn lại sau cắt', async () => {
    mockLines = [material(1, 60, 40)];
    open();
    await choose(0, 'LC-7');

    expect(mockReturnable).toHaveBeenLastCalledWith({ warehouseType: 'MATERIAL', workOrderId: 7 });
    expect(await screen.findByText(RETURN_LABELS.suggestHint)).toBeInTheDocument();
    const row = screen.getByText(/V01 Vải gió/).closest('tr') as HTMLElement;
    expect(within(row).getByRole('spinbutton')).toHaveValue('40');
  });

  it('có bàn cắt chưa nhập số mét thì để trống và báo nhập theo cuộn thực tế', async () => {
    mockLines = [material(1, null)];
    open();
    await choose(0, 'LC-7');

    expect(await screen.findByText(RETURN_LABELS.noSuggestHint)).toBeInTheDocument();
    const row = screen.getByText(/V01 Vải gió/).closest('tr') as HTMLElement;
    expect(within(row).getByRole('spinbutton')).toHaveValue('');
  });

  it('tổ cắt không có lựa chọn trả BTP', () => {
    open();

    expect(screen.queryByText('BTP')).not.toBeInTheDocument();
  });
});

describe('ReturnCreateModal – tổ may', () => {
  beforeEach(() => signIn('Sewing'));

  it('trả NVL không cần chọn đơn: tải ngay danh sách NVL đã nhận', () => {
    mockLines = [material(1, null)];
    open();

    expect(mockReturnable).toHaveBeenLastCalledWith({ warehouseType: 'MATERIAL' });
    expect(screen.getByText(RETURN_LABELS.materialHintSewing)).toBeInTheDocument();
  });

  it('chuyển sang BTP thì phải chọn đơn hàng, chọn đơn rồi tải dòng đơn', async () => {
    mockLines = [piece];
    open();
    fireEvent.click(screen.getByText('BTP'));

    expect(await screen.findByText(RETURN_LABELS.pickOrder)).toBeInTheDocument();
    await choose(0, 'DH4');

    expect(mockReturnable).toHaveBeenLastCalledWith({ warehouseType: 'SEMI_FINISHED', orderId: 4 });
    expect(await screen.findByText(/MH3 Đen\/M/)).toBeInTheDocument();
  });

  it('thêm phế phẩm: gửi kèm dòng phế phẩm, không cần chọn gì khác', async () => {
    open();
    fireEvent.click(screen.getByText(RETURN_LABELS.scrapAdd));
    await choose(0, 'PP-0001 Vải vụn');
    fireEvent.change(screen.getByLabelText(RETURN_LABELS.returnQty), { target: { value: '2,5' } });
    fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.confirm }));
    // Hộp xác nhận tĩnh của antd không có locale trong test nên nút là "OK".
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }));

    await waitFor(() => expect(mockCreate).toHaveBeenCalled());
    const body = mockCreate.mock.calls[0]?.[0] as CreateReturnPayload;
    expect(body).toMatchObject({
      warehouseType: 'MATERIAL',
      lines: [{ materialId: 9, returnedQty: 2.5 }],
    });
  });
});
