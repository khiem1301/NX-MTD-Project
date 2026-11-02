import { fireEvent, render, screen } from '@testing-library/react';
import { MyDefectsPage } from './MyDefectsPage';

const mockUseMyDefects = jest.fn((_query: { status?: string }) => ({
  data: [],
  isFetching: false,
  error: null,
}));

jest.mock('../hooks/useProduction', () => ({
  useMyDefects: (query: { status?: string }) => mockUseMyDefects(query),
  useObjectDefect: () => ({ mutate: jest.fn(), isPending: false }),
}));

describe('MyDefectsPage', () => {
  it('lọc theo trạng thái ở server, không có lựa chọn Đã hủy', async () => {
    render(<MyDefectsPage />);
    expect(mockUseMyDefects).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 100,
      status: undefined,
    });

    fireEvent.mouseDown(screen.getAllByRole('combobox')[0] as HTMLElement);
    expect(screen.queryByText('Đã hủy')).not.toBeInTheDocument();
    fireEvent.click(await screen.findByText('Đã chấp nhận'));

    expect(mockUseMyDefects).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 100,
      status: 'Confirmed',
    });
  });
});
