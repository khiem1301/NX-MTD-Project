import { fireEvent, render, screen } from '@testing-library/react';
import { formatCurrency } from '@/utils/format';
import { PurchaseOrderFormModal } from './PurchaseOrderFormModal';

const mockSuppliers = { data: [], isLoading: false, isSuccess: false };
const mockMaterials = { data: undefined };

jest.mock('../hooks/usePurchaseOrders', () => ({
  useCreatePurchaseOrder: () => ({ mutate: jest.fn(), isPending: false }),
  useSupplierOptions: () => mockSuppliers,
}));
jest.mock('../hooks/useOrderMaterials', () => ({ useOrderMaterials: () => mockMaterials }));
jest.mock('@/components/common/OrderSelect', () => ({ OrderSelect: () => null }));
jest.mock('./MaterialFreeSelect', () => ({ MaterialFreeSelect: () => null }));

describe('PurchaseOrderFormModal: tổng tiền', () => {
  it('tính lại tổng tiền khi đổi số mua và đơn giá', () => {
    render(<PurchaseOrderFormModal open onClose={jest.fn()} />);
    const inputs = screen.getAllByRole('spinbutton');
    const [qty, price] = inputs.slice(-2) as [HTMLElement, HTMLElement];

    fireEvent.change(qty, { target: { value: '400' } });
    fireEvent.change(price, { target: { value: '200' } });

    expect(screen.getByTestId('purchase-order-total')).toHaveTextContent(
      formatCurrency(80000).replace(/\s/g, ' '),
    );
  });
});
