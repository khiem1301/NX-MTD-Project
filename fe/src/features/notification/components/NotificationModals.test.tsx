import { act, render, screen } from '@testing-library/react';
import { useNotificationModal } from '../hooks/useNotificationModal';
import { NotificationModals } from './NotificationModals';

const mockUser = { role: 'Manager' as string };

jest.mock('@/hooks/useAuth', () => ({ useAuth: () => ({ user: mockUser }) }));
jest.mock('@/app/featureSlots', () => ({
  KpiMismatchModalSlot: ({
    role,
    kpiTargetId,
    onClose,
  }: {
    role: string;
    kpiTargetId: number;
    onClose: () => void;
  }) => (
    <button type="button" onClick={onClose}>
      {`chi tiết ${kpiTargetId} (${role})`}
    </button>
  ),
}));

describe('NotificationModals', () => {
  beforeEach(() => useNotificationModal.setState({ mismatchTargetId: null }));

  it('không hiện gì khi chưa mở thông báo nào', () => {
    render(<NotificationModals />);

    expect(screen.queryByText(/chi tiết/)).not.toBeInTheDocument();
  });

  it('Quản đốc mở chi tiết lệch bằng chế độ manager, đóng thì xóa trạng thái', async () => {
    mockUser.role = 'Manager';
    render(<NotificationModals />);

    act(() => useNotificationModal.getState().openMismatch(7));
    const button = await screen.findByText('chi tiết 7 (manager)');
    act(() => button.click());

    expect(useNotificationModal.getState().mismatchTargetId).toBeNull();
  });

  it('tổ trưởng mở chi tiết lệch bằng chế độ team-leader', async () => {
    mockUser.role = 'TeamLeader';
    render(<NotificationModals />);

    act(() => useNotificationModal.getState().openMismatch(9));

    expect(await screen.findByText('chi tiết 9 (team-leader)')).toBeInTheDocument();
  });
});
