import { fireEvent, render, screen, within } from '@testing-library/react';
import { CuttingLayPage } from './CuttingLayPage';

const mockUseLays = jest.fn((_query: { status?: string }) => ({
  data: { items: [], totalItems: 0 },
  isFetching: false,
  error: null,
}));

jest.mock('../hooks/useCutting', () => ({
  useLays: (query: { status?: string }) => mockUseLays(query),
  useWorkOrder: () => ({ data: undefined, error: null }),
  useWorkOrders: () => ({ data: { items: [] }, isLoading: false }),
}));
jest.mock('@/components/common/WorkOrderSelect', () => ({ WorkOrderSelect: () => null }));
jest.mock('../components/LayFormModal', () => ({ LayFormModal: () => null }));
jest.mock('../components/LayDetailModal', () => ({ LayDetailModal: () => null }));

describe('CuttingLayPage', () => {
  it('lọc trạng thái gửi lên server', async () => {
    render(<CuttingLayPage />);
    expect(mockUseLays.mock.lastCall?.[0].status).toBeUndefined();

    fireEvent.mouseDown(screen.getAllByRole('combobox')[0] as HTMLElement);
    const list = await screen.findByRole('listbox');
    fireEvent.click(within(list.parentElement as HTMLElement).getByText('Đã ghi'));

    expect(mockUseLays.mock.lastCall?.[0].status).toBe('RECORDED');
  });
});
