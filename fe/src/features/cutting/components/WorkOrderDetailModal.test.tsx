import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import type { WorkOrder } from '../types';
import { WorkOrderDetailModal } from './WorkOrderDetailModal';

let mockRole = 'Manager';
let mockStatus = 'CUTTING';
const mockClose = jest.fn();

jest.mock('@/hooks/usePermission', () => ({
  usePermission: () => ({ hasRole: (...roles: string[]) => roles.includes(mockRole) }),
}));
jest.mock('../hooks/useCutting', () => ({
  useWorkOrder: () => ({
    data: {
      workOrderId: 7,
      workOrderCode: 'LC-7',
      orderCode: 'DH001',
      productCode: 'AO01',
      productName: 'Áo',
      cuttingTeamName: 'Tổ cắt',
      dueDate: '2026-12-01',
      status: mockStatus,
      note: null,
      lines: [],
      fabrics: [],
      fabricChecks: [],
    } as unknown as WorkOrder,
    isLoading: false,
    error: null,
  }),
  useCancelWorkOrder: () => ({ mutate: jest.fn() }),
  useCloseShortWorkOrder: () => ({ mutate: mockClose, isPending: false }),
}));

const renderModal = () =>
  render(<WorkOrderDetailModal workOrderId={7} onClose={jest.fn()} onEdit={jest.fn()} />);

describe('WorkOrderDetailModal', () => {
  beforeEach(() => {
    mockRole = 'Manager';
    mockStatus = 'CUTTING';
    mockClose.mockReset();
  });

  it('Quản đốc đóng lệnh đang cắt khi cắt thiếu, phải nhập lý do', async () => {
    renderModal();
    fireEvent.click(screen.getByRole('button', { name: LABELS.closeShort }));
    fireEvent.change(await screen.findByRole('textbox'), { target: { value: 'Hết vải' } });
    fireEvent.click(
      screen.getAllByRole('button', { name: MESSAGES.common.confirm }).pop() as HTMLElement,
    );

    await waitFor(() =>
      expect(mockClose).toHaveBeenCalledWith({ id: 7, reason: 'Hết vải' }, expect.anything()),
    );
  });

  it('tổ cắt không còn nút báo cắt xong hay đóng lệnh', () => {
    mockRole = 'TeamLeader';
    renderModal();
    expect(screen.queryByRole('button', { name: LABELS.closeShort })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cắt xong' })).not.toBeInTheDocument();
  });

  it('lệnh chưa cắt thì không có nút đóng lệnh cắt thiếu', () => {
    mockStatus = 'OPEN';
    renderModal();
    expect(screen.queryByRole('button', { name: LABELS.closeShort })).not.toBeInTheDocument();
  });
});
