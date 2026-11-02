import { fireEvent, render, screen } from '@testing-library/react';
import { SyncSessionTable } from './SyncSessionTable';

const mockUseSyncSessions = jest.fn((_status?: string) => ({
  data: [],
  isLoading: false,
  error: null,
}));

jest.mock('../hooks/useAttendance', () => ({
  useSyncSessions: (status?: string) => mockUseSyncSessions(status),
}));

describe('SyncSessionTable', () => {
  it('chọn kết quả thì lấy lại danh sách theo kết quả đó, câu trống không báo chưa từng đồng bộ', async () => {
    render(<SyncSessionTable />);
    expect(mockUseSyncSessions).toHaveBeenLastCalledWith(undefined);
    expect(screen.getAllByText('Chưa từng đồng bộ').length).toBeGreaterThan(0);

    fireEvent.mouseDown(screen.getAllByRole('combobox')[0] as HTMLElement);
    fireEvent.click(await screen.findByText('Lỗi'));

    expect(mockUseSyncSessions).toHaveBeenLastCalledWith('Failed');
    expect(screen.queryByText('Chưa từng đồng bộ')).not.toBeInTheDocument();
  });
});
