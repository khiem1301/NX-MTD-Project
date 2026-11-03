import { fireEvent, render, screen } from '@testing-library/react';
import { LABELS } from '../constants';
import type { OrderMaterial, OrderProductMaterials } from '../types';
import { OrderMaterialByProductModal } from './OrderMaterialByProductModal';

const mockMutate = jest.fn();
let mockProducts: OrderProductMaterials[] = [];
jest.mock('../hooks/useOrderMaterials', () => ({
  useOrderProductMaterials: () => ({ data: mockProducts, isLoading: false }),
  useCreateOrderMaterials: () => ({ mutate: mockMutate, isPending: false }),
}));

const material = (materialId: number, materialCode: string) => ({
  materialId,
  materialCode,
  materialName: `Tên ${materialCode}`,
  materialType: 'FABRIC',
  unit: 'm',
  color: '',
});
const product = (materials = [material(1, 'VAI01'), material(2, 'CHI02')]) => ({
  productId: 7,
  productCode: 'AO-01',
  productName: 'Áo',
  materials,
});

const renderModal = (declared: Partial<OrderMaterial>[] = []) =>
  render(
    <OrderMaterialByProductModal
      orderId={5}
      declared={declared as OrderMaterial[]}
      onClose={jest.fn()}
    />,
  );
const saveButton = () => screen.getByRole('button', { name: /Lưu/ });

describe('OrderMaterialByProductModal', () => {
  beforeEach(() => {
    mockMutate.mockClear();
    mockProducts = [product()];
  });

  it('đơn có một mã hàng thì tự chọn và liệt kê NVL của mã đó', () => {
    renderModal();

    expect(screen.getByText('VAI01')).toBeInTheDocument();
    expect(screen.getByText('CHI02')).toBeInTheDocument();
  });

  it('đơn có nhiều mã hàng thì chờ chọn mã, chưa hiện NVL', () => {
    mockProducts = [product(), { ...product(), productId: 8, productCode: 'AO-02' }];
    renderModal();

    expect(screen.queryByText('VAI01')).not.toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('NVL đơn đã khai báo thì không hiện lại', () => {
    renderModal([{ materialId: 1 }]);

    expect(screen.queryByText('VAI01')).not.toBeInTheDocument();
    expect(screen.getByText('CHI02')).toBeInTheDocument();
  });

  it('đã khai báo đủ thì báo đủ và không cho lưu', () => {
    renderModal([{ materialId: 1 }, { materialId: 2 }]);

    expect(screen.getByText(LABELS.productAllDeclared)).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('mã hàng chưa có định mức thì báo chưa có', () => {
    mockProducts = [product([])];
    renderModal();

    expect(screen.getByText(LABELS.productNoNorm)).toBeInTheDocument();
  });

  it('còn dòng chưa nhập số thì không lưu', () => {
    renderModal();

    fireEvent.click(saveButton());

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('bỏ NVL không dùng, nhập số cho NVL còn lại rồi lưu đúng dòng', () => {
    renderModal();

    const [firstDelete] = screen.getAllByRole('button', { name: /Xóa/ });
    fireEvent.click(firstDelete as HTMLElement);
    const [requiredInput] = screen.getAllByRole('spinbutton');
    fireEvent.change(requiredInput as HTMLElement, { target: { value: '12' } });
    fireEvent.click(saveButton());

    expect(mockMutate).toHaveBeenCalledTimes(1);
    expect(mockMutate.mock.calls[0]?.[0]).toEqual({
      orderId: 5,
      lines: [
        {
          materialId: 2,
          requiredQty: 12,
          customerQty: 0,
          expectedDeliveryCount: null,
          note: undefined,
        },
      ],
    });
  });
});
