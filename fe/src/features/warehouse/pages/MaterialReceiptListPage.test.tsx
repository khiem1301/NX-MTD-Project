import { render, screen } from '@testing-library/react';
import { ROLES } from '@/constants/roles';
import { LABELS } from '../constants';
import { MaterialReceiptListPage } from './MaterialReceiptListPage';

let mockRole: string = ROLES.Manager;

jest.mock('@/components/guards/Can', () => ({
  Can: ({ roles, children }: { roles: string[]; children: React.ReactNode }) =>
    roles.includes(mockRole) ? <>{children}</> : null,
}));
jest.mock('../hooks/useReceipts', () => ({
  useReceipts: () => ({ data: { items: [], totalItems: 0 }, isFetching: false, error: null }),
}));
jest.mock('../components/ReceiptDetailModal', () => ({ ReceiptDetailModal: () => null }));
jest.mock('../components/ReceiptFormModal', () => ({ ReceiptFormModal: () => null }));
jest.mock('../components/PurchaseOrderFormModal', () => ({ PurchaseOrderFormModal: () => null }));

const purchaseButton = () =>
  screen.queryByRole('button', { name: new RegExp(LABELS.createStockPurchase) });

describe('MaterialReceiptListPage: nút tạo đơn mua', () => {
  it.each([ROLES.Admin, ROLES.Manager])('%s thấy nút', (role) => {
    mockRole = role;
    render(<MaterialReceiptListPage />);
    expect(purchaseButton()).toBeInTheDocument();
  });

  it('Thủ kho không thấy nút, chỉ lập phiếu nhập', () => {
    mockRole = ROLES.WarehouseKeeper;
    render(<MaterialReceiptListPage />);
    expect(purchaseButton()).not.toBeInTheDocument();
  });
});
