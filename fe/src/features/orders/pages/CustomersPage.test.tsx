import { fireEvent, render, screen } from '@testing-library/react';
import { CustomersPage } from './CustomersPage';

const useCustomers = jest.fn();
jest.mock('../hooks/useOrders', () => ({
  useCustomers: (query: unknown) => useCustomers(query) as unknown,
  useToggleCustomer: () => ({ mutate: jest.fn(), isPending: false }),
  useSaveCustomer: () => ({ mutate: jest.fn(), isPending: false }),
}));

const customer = (id: number) => ({
  customerId: id,
  customerName: `Khách ${id}`,
  phone: '0901234567',
  email: 'a@b.vn',
  defaultAddress: '12 Lê Lợi',
  status: 'Active',
  discontinueReason: null,
});

const page = (items: unknown[], current = 1, total = 45) => ({
  data: { items, page: current, pageSize: 20, totalItems: total, totalPages: 3 },
  isFetching: false,
  error: null,
  refetch: jest.fn(),
});

describe('CustomersPage', () => {
  beforeEach(() => useCustomers.mockReset());

  it('chỉ hiện các dòng của trang hiện tại và gửi page, pageSize lên máy chủ', () => {
    useCustomers.mockReturnValue(page([customer(1), customer(2)]));

    render(<CustomersPage />);

    expect(screen.getByText('Khách 1')).toBeInTheDocument();
    expect(screen.getByText('Khách 2')).toBeInTheDocument();
    expect(useCustomers).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20 }),
    );
  });

  it('chuyển sang trang 2 thì hỏi lại máy chủ với page = 2, không lọc ở trình duyệt', () => {
    useCustomers.mockReturnValue(page([customer(1)]));
    render(<CustomersPage />);

    fireEvent.click(screen.getByTitle('2'));

    expect(useCustomers).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 2, pageSize: 20 }),
    );
  });

  it('tổng số trang tính theo tổng của máy chủ, không theo số dòng đang có', () => {
    useCustomers.mockReturnValue(page([customer(1)], 1, 45));

    render(<CustomersPage />);

    expect(screen.getByTitle('3')).toBeInTheDocument();
    expect(screen.queryByTitle('4')).not.toBeInTheDocument();
  });
});
