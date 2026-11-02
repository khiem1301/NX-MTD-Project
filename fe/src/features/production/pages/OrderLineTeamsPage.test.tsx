import { fireEvent, render, screen, within } from '@testing-library/react';
import { message } from 'antd';
import type { OrderLineTeamRow } from '../types';
import { OrderLineTeamsPage } from './OrderLineTeamsPage';

const mockSave = jest.fn();
const mockAdd = jest.fn();
const mockParams = jest.fn();
const row = (over: Partial<OrderLineTeamRow> = {}): OrderLineTeamRow => ({
  orderLineId: 11,
  orderId: 1,
  orderCode: 'DH001',
  dueDate: '2026-11-20',
  productId: 3,
  productCode: 'AO01',
  productName: 'Áo khoác',
  color: 'Đỏ',
  sizeCode: 'M',
  orderedQty: 500,
  teams: [],
  ...over,
});
let mockRows: OrderLineTeamRow[] = [];

jest.mock('../hooks/useProduction', () => ({
  useOrderLineTeams: (params: unknown) => {
    mockParams(params);
    return {
      data: { items: mockRows, totalItems: mockRows.length },
      isLoading: false,
      error: null,
    };
  },
  useSetOrderLineTeams: () => ({ mutate: mockSave, isPending: false }),
  useAddOrderLineTeams: () => ({ mutate: mockAdd, isPending: false }),
}));
jest.mock('@/hooks/useLookups', () => ({
  useTeamOptions: () => ({
    data: [
      { teamId: 5, teamName: 'Tổ may 1' },
      { teamId: 6, teamName: 'Tổ may 2' },
    ],
    isLoading: false,
  }),
}));

const teamBox = () => screen.getByRole('combobox', { name: 'Tổ may của DH001 - AO01 - Đỏ/M' });
const rowOf = (text: string) => screen.getByText(text).closest('tr') as HTMLElement;

describe('OrderLineTeamsPage', () => {
  beforeEach(() => {
    mockSave.mockClear();
    mockAdd.mockClear();
    mockParams.mockClear();
    mockRows = [row()];
  });

  it('hiện đơn, mã hàng, màu, size và số lượng của từng dòng', () => {
    render(<OrderLineTeamsPage />);

    const cells = within(rowOf('AO01'));
    expect(cells.getByText('DH001')).toBeInTheDocument();
    expect(cells.getByText('Đỏ')).toBeInTheDocument();
    expect(cells.getByText('M')).toBeInTheDocument();
    expect(cells.getByText('500')).toBeInTheDocument();
  });

  it('chọn tổ thì lưu danh sách tổ của đúng dòng đơn', async () => {
    render(<OrderLineTeamsPage />);

    fireEvent.mouseDown(teamBox());
    fireEvent.click(await screen.findByTitle('Tổ may 2'));

    expect(mockSave).toHaveBeenCalledWith({ orderLineId: 11, teamIds: [6] });
  });

  it('chọn thêm tổ thì giữ tổ đã chia', async () => {
    mockRows = [row({ teams: [{ teamId: 5, teamName: 'Tổ may 1' }] })];
    render(<OrderLineTeamsPage />);

    fireEvent.mouseDown(teamBox());
    fireEvent.click(await screen.findByTitle('Tổ may 2'));

    expect(mockSave).toHaveBeenCalledWith({ orderLineId: 11, teamIds: [5, 6] });
  });

  it('tổ đã chia nhưng ngừng hoạt động vẫn hiện, có ghi chú', () => {
    mockRows = [row({ teams: [{ teamId: 9, teamName: 'Tổ may cũ' }] })];
    render(<OrderLineTeamsPage />);

    expect(screen.getByText('Tổ may cũ (ngừng hoạt động)')).toBeInTheDocument();
  });

  it('tích chỉ dòng chưa chia thì lọc và về trang 1', () => {
    render(<OrderLineTeamsPage />);

    fireEvent.click(screen.getByRole('checkbox', { name: 'Chỉ dòng chưa chia' }));

    expect(mockParams).toHaveBeenLastCalledWith(
      expect.objectContaining({ onlyUnallotted: true, page: 1 }),
    );
  });

  describe('chia nhiều dòng một lần', () => {
    beforeEach(() => {
      mockRows = [
        row(),
        row({ orderLineId: 12, productCode: 'AO02', color: 'Xanh', sizeCode: 'L' }),
      ];
    });
    const tick = (text: string) =>
      fireEvent.click(within(rowOf(text)).getAllByRole('checkbox')[0]!);
    const pickTeam = async () => {
      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn tổ may' }));
      const dropdowns = await screen.findAllByTitle('Tổ may 2');
      fireEvent.click(dropdowns[dropdowns.length - 1]!);
    };

    it('chưa tích dòng thì không có thanh chia', () => {
      render(<OrderLineTeamsPage />);

      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
    });

    it('gửi các dòng đã tích, báo số lượt chia rồi bỏ chọn', async () => {
      const success = jest.spyOn(message, 'success').mockImplementation();
      mockAdd.mockImplementation((_: unknown, options: { onSuccess: (n: number) => void }) =>
        options.onSuccess(2),
      );
      render(<OrderLineTeamsPage />);
      tick('AO01');
      tick('AO02');
      expect(screen.getByText(/Đã chọn 2 dòng/)).toBeInTheDocument();

      await pickTeam();
      fireEvent.click(screen.getByRole('button', { name: 'Chia' }));

      expect(mockAdd).toHaveBeenCalledWith(
        { orderLineIds: [11, 12], teamIds: [6] },
        expect.anything(),
      );
      expect(success).toHaveBeenCalledWith('Đã thêm 2 lượt chia.');
      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
      success.mockRestore();
    });

    it('dòng đã chia hết cho tổ thì báo không có gì thay đổi', async () => {
      const info = jest.spyOn(message, 'info').mockImplementation();
      mockAdd.mockImplementation((_: unknown, options: { onSuccess: (n: number) => void }) =>
        options.onSuccess(0),
      );
      render(<OrderLineTeamsPage />);
      tick('AO01');

      await pickTeam();
      fireEvent.click(screen.getByRole('button', { name: 'Chia' }));

      expect(info).toHaveBeenCalledWith(expect.stringContaining('không có gì thay đổi'));
      info.mockRestore();
    });

    it('chưa chọn tổ thì chưa chia được', () => {
      render(<OrderLineTeamsPage />);
      tick('AO01');

      expect(screen.getByRole('button', { name: 'Chia' })).toBeDisabled();
    });

    it('đổi bộ lọc thì bỏ các dòng đã tích', () => {
      render(<OrderLineTeamsPage />);
      tick('AO01');

      fireEvent.click(screen.getByRole('checkbox', { name: 'Chỉ dòng chưa chia' }));

      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
    });
  });
});
