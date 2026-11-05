import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { Modal, message } from 'antd';
import type { Operation, OperationGroup } from '../types';
import { OperationsPage } from './OperationsPage';

const mockSetGroup = jest.fn();
const mockSubmitRates = jest.fn();
const mockCreateGroup = jest.fn();
const mockRename = jest.fn();
const mockDelete = jest.fn();
let mockRole: 'accountant' | 'admin' = 'accountant';
const op = (operationId: number, over: Partial<Operation> = {}) =>
  ({
    operationId,
    productId: 1,
    operationCode: 'OP0' + operationId,
    operationName: 'Công đoạn ' + operationId,
    stationSequence: operationId,
    skillLevel: 1,
    standardTimeSec: 10,
    teamType: 'Sewing',
    isFinalStep: false,
    status: 'Active',
    currentRate: 100,
    nextRate: null,
    upcomingRate: null,
    operationGroupId: null,
    groupName: null,
    componentIds: [],
    ...over,
  }) as Operation;
let mockOperations: Operation[] = [];
let mockGroups: OperationGroup[] = [];

jest.mock('../hooks/useOperationRole', () => ({ useOperationRole: () => mockRole }));
jest.mock('../hooks/usePieceRates', () => ({
  useSubmitPieceRate: () => ({ mutate: jest.fn() }),
  useSubmitPieceRates: () => ({ mutate: mockSubmitRates, isPending: false }),
}));
jest.mock('../hooks/useOperations', () => ({
  useOperations: () => ({ data: mockOperations, isSuccess: true, isFetching: false, error: null }),
  useOperationGroups: () => ({ data: mockGroups, isLoading: false, error: null }),
  useToggleOperation: () => ({ mutate: jest.fn() }),
  useSaveOperation: () => ({ mutate: jest.fn(), isPending: false }),
  useSetOperationsGroup: () => ({ mutate: mockSetGroup, isPending: false }),
  useCreateOperationGroup: () => ({ mutate: mockCreateGroup, isPending: false }),
  useRenameOperationGroup: () => ({ mutate: mockRename, isPending: false }),
  useDeleteOperationGroup: () => ({ mutate: mockDelete, isPending: false }),
}));
jest.mock('@/hooks/useOrderLookups', () => ({
  useProductLookup: () => ({
    data: { items: [{ productId: 1, productCode: 'AO', productName: 'Áo' }] },
  }),
}));
jest.mock('@/features/orders', () => ({
  useProductSpec: () => ({ data: undefined, isLoading: false }),
}));
jest.mock('../components/ProductSelect', () => ({
  ProductSelect: ({ onChange }: { onChange: (id: number) => void }) => (
    <button onClick={() => onChange(1)}>chọn mã</button>
  ),
}));
jest.mock('../components/PieceRateFormModal', () => ({ PieceRateFormModal: () => null }));
jest.mock('../components/PieceRateHistory', () => ({ PieceRateHistoryModal: () => null }));
jest.mock('../components/OperationImportModal', () => ({
  OperationImportModal: ({ allowRates }: { allowRates: boolean }) => (
    <div>nhập excel, đơn giá: {String(allowRates)}</div>
  ),
}));

const open = () => {
  render(<OperationsPage />);
  fireEvent.click(screen.getByText('chọn mã'));
};
const rowOf = (name: string) => screen.getByText(name).closest('tr') as HTMLElement;
const tick = (name: string) => fireEvent.click(within(rowOf(name)).getAllByRole('checkbox')[0]!);
const group = (
  operationGroupId: number,
  groupName: string,
  operationCount = 1,
): OperationGroup => ({
  operationGroupId,
  groupName,
  operationCount,
});

describe('OperationsPage', () => {
  beforeEach(() => {
    [mockSetGroup, mockSubmitRates, mockCreateGroup, mockRename, mockDelete].forEach((m) =>
      m.mockReset(),
    );
    mockRole = 'accountant';
    mockOperations = [
      op(1, { operationGroupId: 10, groupName: 'May tay' }),
      op(2),
      op(3, { isFinalStep: true }),
    ];
    mockGroups = [group(10, 'May tay'), group(20, 'May cổ', 0)];
  });

  it('báo số công đoạn và số dòng chưa có nhóm', () => {
    open();

    expect(
      screen.getByText('3 công đoạn đang dùng, 2 chưa có nhóm, 0 chưa có đơn giá được duyệt.'),
    ).toBeInTheDocument();
  });

  it('chưa chọn mã hàng thì hướng dẫn, chưa hiện bảng', () => {
    render(<OperationsPage />);

    expect(screen.getByText(/Chọn mã hàng để xem/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('đơn giá chưa duyệt hiện rõ Chưa có; công đoạn đã ngừng thì không', () => {
    mockOperations = [
      op(1, { currentRate: null }),
      op(2, { currentRate: null, status: 'Discontinued' }),
    ];
    open();

    expect(within(rowOf('Công đoạn 1')).getByText('Chưa có')).toBeInTheDocument();
    expect(within(rowOf('Công đoạn 2')).queryByText('Chưa có')).not.toBeInTheDocument();
    expect(within(rowOf('Công đoạn 2')).getByText('Đã ngừng')).toBeInTheDocument();
  });

  it('cột Đơn giá chỉ hiện đơn giá đã duyệt sắp áp dụng, không hiện bản nháp', () => {
    const rate = (pieceRateId: number, value: number, status: 'Approved' | 'Draft') => ({
      pieceRateId,
      rate: value,
      effectiveDate: '2026-12-01',
      status,
      rejectReason: null,
    });
    mockOperations = [
      op(1, { upcomingRate: rate(21, 150, 'Approved') }),
      op(2, { nextRate: rate(22, 170, 'Draft') }),
    ];
    open();

    expect(within(rowOf('Công đoạn 1')).getByText('→ 150 ₫ từ 01/12/2026')).toBeInTheDocument();
    expect(
      within(rowOf('Công đoạn 2')).queryByText('→ 150 ₫ từ 01/12/2026'),
    ).not.toBeInTheDocument();
    expect(within(rowOf('Công đoạn 2')).queryByText('Nháp')).not.toBeInTheDocument();
  });

  it('công đoạn cuối đang dùng ngừng được, hộp xác nhận cảnh báo mã hàng sẽ không còn công đoạn cuối', async () => {
    mockOperations = [op(1, { isFinalStep: true }), op(2)];
    open();

    fireEvent.click(within(rowOf('Công đoạn 1')).getByRole('button', { name: 'Ngừng dùng' }));
    expect(await screen.findByText(/không còn công đoạn cuối/)).toBeInTheDocument();
    act(() => Modal.destroyAll());
  });

  describe('thêm công đoạn', () => {
    const openAdd = () => {
      open();
      fireEvent.click(screen.getByRole('button', { name: /Thêm công đoạn/ }));
    };

    it('gợi ý sẵn mã và thứ tự tiếp theo', () => {
      openAdd();

      expect(screen.getByRole('textbox', { name: 'Mã công đoạn' })).toHaveValue('OP04');
      expect(screen.getByRole('spinbutton', { name: 'Thứ tự' })).toHaveValue('4');
    });

    it('mã có dấu hoặc khoảng trắng thì báo lỗi', async () => {
      openAdd();

      fireEvent.change(screen.getByRole('textbox', { name: 'Mã công đoạn' }), {
        target: { value: 'Công đoạn 1' },
      });

      expect(await screen.findByText(/Mã chỉ gồm chữ không dấu/)).toBeInTheDocument();
    });
  });

  describe('gửi duyệt đơn giá Nháp', () => {
    const draft = (pieceRateId: number) =>
      ({
        pieceRateId,
        rate: 200,
        effectiveDate: '2026-11-10',
        status: 'Draft',
        rejectReason: null,
      }) as Operation['nextRate'];

    it('Kế toán: chỉ gửi đơn giá Nháp của các dòng đã tích', async () => {
      mockOperations = [op(1, { nextRate: draft(11) }), op(2, { nextRate: draft(12) }), op(3)];
      open();
      tick('Công đoạn 1');
      tick('Công đoạn 3');

      fireEvent.click(screen.getByRole('button', { name: 'Gửi duyệt 1 đơn giá Nháp' }));
      fireEvent.click(await screen.findByRole('button', { name: /Xác nhận|Đồng ý|OK/ }));

      expect(mockSubmitRates).toHaveBeenCalledWith([11], expect.anything());
    });

    it('dòng đã tích không có Nháp thì không hiện nút', () => {
      open();
      tick('Công đoạn 2');

      expect(screen.queryByRole('button', { name: /Gửi duyệt/ })).not.toBeInTheDocument();
    });

    it('Admin không có nút gửi duyệt hàng loạt', () => {
      mockRole = 'admin';
      mockOperations = [op(1, { nextRate: draft(11) })];
      open();
      tick('Công đoạn 1');

      expect(screen.queryByRole('button', { name: /Gửi duyệt/ })).not.toBeInTheDocument();
    });
  });

  it('chưa chọn mã hàng thì khóa các nút thao tác', () => {
    render(<OperationsPage />);

    expect(screen.getByRole('button', { name: /Nhóm công đoạn/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Nhập từ Excel/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Thêm công đoạn/ })).toBeDisabled();
  });

  describe('quyền đơn giá', () => {
    it('Kế toán có nút đặt đơn giá', () => {
      open();

      expect(
        within(rowOf('Công đoạn 2')).getByRole('button', { name: 'Đặt đơn giá' }),
      ).toBeInTheDocument();
    });

    it('Admin không có nút đặt, gửi duyệt đơn giá nhưng vẫn thấy đơn giá hiện hành', () => {
      mockRole = 'admin';
      mockOperations = [
        op(2, {
          nextRate: {
            pieceRateId: 1,
            rate: 200,
            effectiveDate: '2026-11-10',
            status: 'Draft',
            rejectReason: null,
          },
        }),
      ];
      open();

      const row = within(rowOf('Công đoạn 2'));
      expect(row.queryByRole('button', { name: 'Đặt đơn giá' })).not.toBeInTheDocument();
      expect(row.queryByRole('button', { name: /Sửa đơn giá/ })).not.toBeInTheDocument();
      expect(row.queryByRole('button', { name: /Gửi/ })).not.toBeInTheDocument();
      expect(row.getByRole('button', { name: 'Sửa' })).toBeInTheDocument();
    });

    it.each([
      ['accountant', 'true'],
      ['admin', 'false'],
    ] as const)('nhập Excel của %s: đọc đơn giá = %s', (role, allowed) => {
      mockRole = role;
      open();

      fireEvent.click(screen.getByRole('button', { name: /Nhập từ Excel/ }));

      expect(screen.getByText('nhập excel, đơn giá: ' + allowed)).toBeInTheDocument();
    });
  });

  describe('lọc', () => {
    it('tìm kiếm thu hẹp bảng', () => {
      open();

      fireEvent.change(screen.getByPlaceholderText('Tìm mã, tên'), {
        target: { value: 'OP02' },
      });

      expect(screen.getByText('Công đoạn 2')).toBeInTheDocument();
      expect(screen.queryByText('Công đoạn 1')).not.toBeInTheDocument();
    });

    it('đổi mã hàng thì xóa từ khóa và bỏ dòng đã tích', () => {
      open();
      tick('Công đoạn 2');
      fireEvent.change(screen.getByPlaceholderText('Tìm mã, tên'), {
        target: { value: 'OP02' },
      });

      fireEvent.click(screen.getByText('chọn mã'));

      expect(screen.getByText('Công đoạn 1')).toBeInTheDocument();
      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
    });
  });

  describe('xếp nhóm nhiều công đoạn', () => {
    const pickGroup = async () => {
      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn nhóm' }));
      const options = await screen.findAllByTitle('May cổ');
      fireEvent.click(options[options.length - 1]!);
    };

    it('xếp các dòng đã tích vào nhóm rồi bỏ chọn', async () => {
      const success = jest.spyOn(message, 'success').mockImplementation();
      mockSetGroup.mockImplementation((_: unknown, o: { onSuccess: (n: number) => void }) =>
        o.onSuccess(2),
      );
      open();
      tick('Công đoạn 1');
      tick('Công đoạn 2');

      await pickGroup();
      fireEvent.click(screen.getByRole('button', { name: 'Xếp nhóm' }));

      expect(mockSetGroup).toHaveBeenCalledWith(
        { productId: 1, operationIds: [1, 2], operationGroupId: 20 },
        expect.anything(),
      );
      expect(success).toHaveBeenCalledWith('Đã đổi nhóm 2 công đoạn.');
      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
      success.mockRestore();
    });

    it('bỏ nhóm gửi nhóm rỗng, chưa chọn nhóm thì chưa xếp được', () => {
      open();
      tick('Công đoạn 1');

      expect(screen.getByRole('button', { name: 'Xếp nhóm' })).toBeDisabled();
      fireEvent.click(screen.getByRole('button', { name: 'Bỏ nhóm' }));

      expect(mockSetGroup).toHaveBeenCalledWith(
        { productId: 1, operationIds: [1], operationGroupId: null },
        expect.anything(),
      );
    });

    it('không có gì đổi thì báo rõ', () => {
      const info = jest.spyOn(message, 'info').mockImplementation();
      mockSetGroup.mockImplementation((_: unknown, o: { onSuccess: (n: number) => void }) =>
        o.onSuccess(0),
      );
      open();
      tick('Công đoạn 2');

      fireEvent.click(screen.getByRole('button', { name: 'Bỏ nhóm' }));

      expect(info).toHaveBeenCalledWith(expect.stringContaining('không có gì thay đổi'));
      info.mockRestore();
    });
  });

  describe('hộp thoại nhóm công đoạn', () => {
    const openGroups = () => {
      open();
      fireEvent.click(screen.getByRole('button', { name: /Nhóm công đoạn/ }));
      return within(screen.getByRole('dialog'));
    };

    it('liệt kê nhóm và số công đoạn, kể cả nhóm chưa có công đoạn', () => {
      const dialog = openGroups();

      expect(dialog.getByText('Nhóm công đoạn của AO - Áo')).toBeInTheDocument();
      expect(
        within(dialog.getByText('May cổ').closest('tr') as HTMLElement).getByText('0'),
      ).toBeInTheDocument();
    });

    it('thêm nhóm: cắt khoảng trắng, tên trống thì khóa nút', () => {
      mockCreateGroup.mockImplementation((_: unknown, o: { onSuccess: () => void }) =>
        o.onSuccess(),
      );
      const dialog = openGroups();
      const input = dialog.getByRole('textbox', { name: /Tên nhóm mới/ });

      expect(dialog.getByRole('button', { name: 'Thêm nhóm' })).toBeDisabled();
      fireEvent.change(input, { target: { value: '   ' } });
      expect(dialog.getByRole('button', { name: 'Thêm nhóm' })).toBeDisabled();

      fireEvent.change(input, { target: { value: '  May túi ' } });
      fireEvent.click(dialog.getByRole('button', { name: 'Thêm nhóm' }));

      expect(mockCreateGroup).toHaveBeenCalledWith(
        { productId: 1, groupName: 'May túi' },
        expect.anything(),
      );
      expect(input).toHaveValue('');
    });

    it('đổi tên ngay trên dòng', () => {
      mockRename.mockImplementation((_: unknown, o: { onSuccess: () => void }) => o.onSuccess());
      const dialog = openGroups();
      const row = within(dialog.getByText('May tay').closest('tr') as HTMLElement);

      fireEvent.click(row.getByRole('button', { name: 'Đổi tên' }));
      const input = dialog.getByRole('textbox', { name: 'Đổi tên' });
      fireEvent.change(input, { target: { value: 'May tay áo' } });
      fireEvent.keyDown(input, { key: 'Enter', keyCode: 13 });

      expect(mockRename).toHaveBeenCalledWith(
        { id: 10, groupName: 'May tay áo' },
        expect.anything(),
      );
      expect(dialog.queryByRole('textbox', { name: 'Đổi tên' })).not.toBeInTheDocument();
    });
  });
});
