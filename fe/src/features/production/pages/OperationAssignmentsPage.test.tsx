import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { message } from 'antd';
import type { OperationAssignment, OperationOption } from '../types';
import { OperationAssignmentsPage } from './OperationAssignmentsPage';

const mockAssign = jest.fn();
const mockAssignMany = jest.fn();
const mockClose = jest.fn();
const operation = (
  operationId: number,
  code: string,
  name: string,
  groupName: string | null = null,
  skillLevel: number | null = null,
  productId = 1,
) =>
  ({
    operationId,
    productId,
    productCode: productId === 1 ? 'AO' : 'QU',
    operationCode: code,
    operationName: name,
    stationSequence: operationId,
    groupName,
    skillLevel,
  }) as OperationOption;
const twoOperations = [operation(1, 'OP1', 'May cổ', 'Cổ'), operation(2, 'OP2', 'May tay', 'Tay')];
const mockOperations = {
  data: twoOperations,
  isSuccess: true,
  isLoading: false,
  error: null,
};
let mockAssignments: OperationAssignment[] = [];
let mockLines: { productId: number; productCode: string }[] = [];
const mockWorkers = {
  options: [
    { value: 7, label: 'CN7 - An' },
    { value: 8, label: 'CN8 - Bình' },
  ],
  loading: false,
};

jest.mock('../hooks/useProduction', () => ({
  useAssignableOperations: () => mockOperations,
  useAllottedOrderLines: () => ({ data: mockLines, error: null }),
  useAssignments: () => ({ data: mockAssignments, isSuccess: true, isLoading: false, error: null }),
  useAssignOperation: () => ({ mutate: mockAssign, isPending: false }),
  useAssignOperations: () => ({ mutate: mockAssignMany, isPending: false }),
  useCloseAssignment: () => ({ mutate: mockClose, isPending: false }),
}));
jest.mock('../hooks/useWorkerOptions', () => ({ useWorkerOptions: () => mockWorkers }));
jest.mock('../components/CopyModal', () => ({ CopyModal: () => null }));
jest.mock('@/components/common/OrderSelect', () => ({
  OrderSelect: ({ onChange }: { onChange: (id: number) => void }) => (
    <button onClick={() => onChange(5)}>chọn đơn</button>
  ),
}));

const assignment = (id: number, operationId: number, workerId: number) =>
  ({
    operationAssignmentId: id,
    operationId,
    workerId,
    workerCode: 'CN' + workerId,
    workerName: 'X',
  }) as OperationAssignment;

const open = () => {
  render(<OperationAssignmentsPage />);
  fireEvent.click(screen.getByText('chọn đơn'));
};
const rowOf = (text: RegExp) => screen.getByText(text).closest('tr') as HTMLElement;

describe('OperationAssignmentsPage', () => {
  beforeEach(() => {
    mockAssign.mockClear();
    mockAssignMany.mockClear();
    mockClose.mockClear();
    mockAssignments = [];
    mockOperations.data = twoOperations;
    mockOperations.isSuccess = true;
    mockLines = [];
  });

  it('chưa chọn đơn thì chỉ hướng dẫn chọn đơn, chưa hiện bảng', () => {
    render(<OperationAssignmentsPage />);

    expect(screen.getByText(/Chọn đơn hàng để đặt công nhân/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('hiện bậc của công đoạn, công đoạn chưa khai bậc thì hiện gạch ngang', () => {
    mockOperations.data = [operation(1, 'OP1', 'May cổ', null, 4), operation(2, 'OP2', 'May tay')];
    open();

    expect(within(rowOf(/OP1 - May cổ/)).getByText('4')).toBeInTheDocument();
    expect(within(rowOf(/OP2 - May tay/)).getByText('—')).toBeInTheDocument();
  });

  describe('đơn có nhiều mã hàng được chia cho tổ', () => {
    beforeEach(() => {
      mockOperations.data = [
        operation(1, 'OP1', 'May cổ', 'Cổ', null, 1),
        operation(2, 'OP2', 'May tay', 'Tay', null, 1),
        operation(3, 'Q1', 'May lưng', 'Lưng', null, 2),
      ];
      mockAssignments = [assignment(1, 3, 7)];
    });
    const pickProduct = async (label: string) => {
      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Mã hàng' }));
      fireEvent.click(await screen.findByTitle(label));
    };

    it('mặc định mã đầu tiên, chỉ hiện công đoạn và tiến độ của mã đó', () => {
      open();

      expect(screen.getByText(/OP1 - May cổ/)).toBeInTheDocument();
      expect(screen.queryByText(/Q1 - May lưng/)).not.toBeInTheDocument();
      expect(screen.getByText('0/2')).toBeInTheDocument();
    });

    it('đổi mã hàng thì đổi công đoạn, tiến độ và bỏ các dòng đã tích', async () => {
      open();
      fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));

      await pickProduct('QU (1/1 công đoạn)');

      expect(screen.getByText(/Q1 - May lưng/)).toBeInTheDocument();
      expect(screen.queryByText(/OP1 - May cổ/)).not.toBeInTheDocument();
      expect(screen.getByText(/Đã phân công đủ 1 công đoạn/)).toBeInTheDocument();
      expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
    });
  });

  describe('mã hàng được chia nhưng chưa khai công đoạn', () => {
    const AO = { productId: 1, productCode: 'AO' };
    const QU = { productId: 2, productCode: 'QU' };
    const warning = /Mã hàng QU chưa có công đoạn may đang dùng/;

    it('hiện trong ô chọn mã và báo ngay, mặc định vẫn mở mã đã có công đoạn', async () => {
      mockLines = [QU, AO];
      open();

      expect(screen.getByText(warning)).toBeInTheDocument();
      expect(screen.getByText(/OP1 - May cổ/)).toBeInTheDocument();
      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Mã hàng' }));
      expect(await screen.findByTitle('QU (chưa khai công đoạn)')).toBeInTheDocument();
    });

    it('chọn mã đó thì bảng trống, không có tiến độ', async () => {
      mockLines = [AO, QU];
      open();

      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Mã hàng' }));
      fireEvent.click(await screen.findByTitle('QU (chưa khai công đoạn)'));

      expect(screen.getByText('Mã hàng này chưa có công đoạn may đang dùng.')).toBeInTheDocument();
      expect(screen.queryByText(/OP1 - May cổ/)).not.toBeInTheDocument();
      expect(screen.queryByText('0/2')).not.toBeInTheDocument();
    });

    it('đơn chỉ có mã đó thì vẫn báo, không có ô chọn mã', () => {
      mockOperations.data = [];
      mockLines = [QU];
      open();

      expect(screen.getByText(warning)).toBeInTheDocument();
      expect(screen.queryByRole('combobox', { name: 'Mã hàng' })).not.toBeInTheDocument();
    });

    it('mã nào cũng có công đoạn thì không báo', () => {
      mockLines = [AO];
      open();

      expect(screen.queryByText(/chưa có công đoạn may đang dùng/)).not.toBeInTheDocument();
    });

    it('chưa tải xong công đoạn thì chưa báo', () => {
      mockOperations.data = [];
      mockOperations.isSuccess = false;
      mockLines = [AO, QU];
      open();

      expect(screen.queryByText(warning)).not.toBeInTheDocument();
    });
  });

  it('đơn chỉ có một mã hàng thì không hiện ô chọn mã', () => {
    open();

    expect(screen.queryByRole('combobox', { name: 'Mã hàng' })).not.toBeInTheDocument();
  });

  it('chưa đủ người thì báo số công đoạn còn thiếu và tiến độ', () => {
    mockAssignments = [assignment(1, 1, 7)];
    open();

    expect(screen.getByText(/Còn 1\/2 công đoạn chưa có người/)).toBeInTheDocument();
    expect(screen.getByText('1/2')).toBeInTheDocument();
  });

  it('đủ người thì báo nhận được BTP', () => {
    mockAssignments = [assignment(1, 1, 7), assignment(2, 2, 8)];
    open();

    expect(screen.getByText(/Đã phân công đủ 2 công đoạn/)).toBeInTheDocument();
  });

  it('lọc chỉ công đoạn chưa có người', () => {
    mockAssignments = [assignment(1, 1, 7)];
    open();

    fireEvent.click(screen.getByText('Chưa có người (1)'));

    expect(screen.queryByText(/OP1 - May cổ/)).not.toBeInTheDocument();
    expect(screen.getByText(/OP2 - May tay/)).toBeInTheDocument();
  });

  it('tìm công đoạn theo tên, không có kết quả thì báo rõ', () => {
    open();
    const search = screen.getByPlaceholderText('Tìm mã, tên');

    fireEvent.change(search, { target: { value: 'tay' } });
    expect(screen.queryByText(/OP1 - May cổ/)).not.toBeInTheDocument();

    fireEvent.change(search, { target: { value: 'không có' } });
    expect(screen.getByText('Không có công đoạn phù hợp với bộ lọc.')).toBeInTheDocument();
  });

  it('chọn công nhân trên dòng thì phân công đúng công đoạn', async () => {
    open();

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Công nhân OP2' }));
    fireEvent.click(await screen.findByTitle('CN8 - Bình'));

    expect(mockAssign).toHaveBeenCalledWith({ orderId: 5, operationId: 2, workerId: 8 });
  });

  it('bỏ chọn công nhân thì đóng đúng phân công', async () => {
    mockAssignments = [assignment(9, 1, 7)];
    open();

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Công nhân OP1' }));
    await screen.findAllByText('CN7 - An');
    fireEvent.click(document.querySelector('.ant-select-item-option-selected') as Element);

    expect(mockClose).toHaveBeenCalledWith(9);
  });

  it('tích chọn nhiều dòng rồi giao một lần cho một công nhân', async () => {
    open();

    fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));
    fireEvent.click(within(rowOf(/OP2 - May tay/)).getByRole('checkbox'));
    expect(screen.getByText(/Đã chọn 2 công đoạn/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Giao việc' })).toBeDisabled();

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn công nhân' }));
    fireEvent.click(await screen.findByTitle('CN7 - An'));
    fireEvent.click(screen.getByRole('button', { name: 'Giao việc' }));

    expect(mockAssignMany.mock.calls[0][0]).toEqual({
      orderId: 5,
      workerId: 7,
      operationIds: [1, 2],
    });
  });

  it('dòng đã tích vừa có người khi đang lọc dòng thiếu thì không còn được giao', () => {
    const { rerender } = render(<OperationAssignmentsPage />);
    fireEvent.click(screen.getByText('chọn đơn'));
    fireEvent.click(screen.getByText('Chưa có người (2)'));
    fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));
    fireEvent.click(within(rowOf(/OP2 - May tay/)).getByRole('checkbox'));
    expect(screen.getByText(/Đã chọn 2 công đoạn/)).toBeInTheDocument();

    mockAssignments = [assignment(1, 1, 7)];
    rerender(<OperationAssignmentsPage />);

    expect(screen.getByText(/Đã chọn 1 công đoạn/)).toBeInTheDocument();
  });

  it('báo số công đoạn đã giao và số bỏ qua vì công nhân đang làm', async () => {
    const success = jest.spyOn(message, 'success').mockImplementation(jest.fn());
    open();
    fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));
    fireEvent.click(within(rowOf(/OP2 - May tay/)).getByRole('checkbox'));
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn công nhân' }));
    fireEvent.click(await screen.findByTitle('CN7 - An'));
    fireEvent.click(screen.getByRole('button', { name: 'Giao việc' }));

    act(() => mockAssignMany.mock.calls[0][1].onSuccess(1));

    expect(success).toHaveBeenCalledWith(
      'Đã giao 1 công đoạn, bỏ qua 1 công đoạn công nhân đang làm.',
    );
    success.mockRestore();
  });

  it('công nhân đã làm hết các công đoạn đã chọn thì báo không có gì thay đổi', async () => {
    const info = jest.spyOn(message, 'info').mockImplementation(jest.fn());
    open();
    fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn công nhân' }));
    fireEvent.click(await screen.findByTitle('CN7 - An'));
    fireEvent.click(screen.getByRole('button', { name: 'Giao việc' }));

    act(() => mockAssignMany.mock.calls[0][1].onSuccess(0));

    expect(info).toHaveBeenCalledWith(
      'Công nhân đã làm tất cả công đoạn đã chọn, không có gì thay đổi.',
    );
    info.mockRestore();
  });

  it('đổi bộ lọc thì bỏ các dòng đã chọn để không giao nhầm dòng bị ẩn', () => {
    open();
    fireEvent.click(within(rowOf(/OP1 - May cổ/)).getByRole('checkbox'));

    fireEvent.click(screen.getByText('Chưa có người (2)'));

    expect(screen.queryByText(/Đã chọn/)).not.toBeInTheDocument();
  });

  describe('phân trang', () => {
    beforeEach(() => {
      mockOperations.data = Array.from({ length: 15 }, (_, i) =>
        operation(i + 1, `M${i + 1}.`, 'Việc'),
      );
    });
    const goToPage2 = () => fireEvent.click(screen.getByTitle('2'));

    it('mỗi trang 10 dòng, trang sau hiện phần còn lại', () => {
      open();

      expect(screen.getByText('M10. - Việc')).toBeInTheDocument();
      expect(screen.queryByText('M11. - Việc')).not.toBeInTheDocument();

      goToPage2();

      expect(screen.getByText('M15. - Việc')).toBeInTheDocument();
      expect(screen.queryByText('M1. - Việc')).not.toBeInTheDocument();
    });

    it('dòng đã tích được giữ khi chuyển trang và cùng được giao', async () => {
      open();
      fireEvent.click(within(rowOf(/M1\. - Việc/)).getByRole('checkbox'));
      goToPage2();
      fireEvent.click(within(rowOf(/M15. - Việc/)).getByRole('checkbox'));
      expect(screen.getByText(/Đã chọn 2 công đoạn/)).toBeInTheDocument();

      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Chọn công nhân' }));
      fireEvent.click(await screen.findByTitle('CN7 - An'));
      fireEvent.click(screen.getByRole('button', { name: 'Giao việc' }));

      expect(mockAssignMany.mock.calls[0][0]).toEqual({
        orderId: 5,
        workerId: 7,
        operationIds: [1, 15],
      });
    });

    it('đổi bộ lọc thì quay về trang 1', () => {
      open();
      goToPage2();

      fireEvent.change(screen.getByPlaceholderText('Tìm mã, tên'), { target: { value: 'việc' } });

      expect(screen.getByText('M1. - Việc')).toBeInTheDocument();
    });
  });
});
