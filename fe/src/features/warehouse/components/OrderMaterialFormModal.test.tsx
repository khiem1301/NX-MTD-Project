import { fireEvent, render, screen } from '@testing-library/react';
import type { OrderMaterial } from '../types';
import { OrderMaterialFormModal } from './OrderMaterialFormModal';

const option = (materialId: number, materialCode: string) => ({
  materialId,
  materialCode,
  materialName: 'Vải',
  materialType: 'FABRIC',
  unit: 'm',
  color: '',
});

jest.mock('@/hooks/useLookups', () => ({
  useMaterialOptions: () => ({
    data: [option(1, 'VAI01'), option(2, 'VAI02'), option(3, 'VAI03')],
    isLoading: false,
  }),
}));
jest.mock('../hooks/useOrderMaterials', () => ({
  useSaveOrderMaterial: () => ({ mutate: jest.fn(), isPending: false }),
}));
jest.mock('./MaterialFormModal', () => ({ MaterialFormModal: () => null }));

const declared = (materialId: number, materialCode: string) =>
  ({
    orderMaterialId: materialId,
    materialId,
    materialCode,
    materialName: 'Vải',
    materialColor: '',
    requiredQty: 1,
    customerQty: 0,
    expectedDeliveryCount: null,
    note: null,
  }) as OrderMaterial;

const openOptions = (orderMaterial: OrderMaterial | null, list: OrderMaterial[]) => {
  render(
    <OrderMaterialFormModal
      open
      orderId={5}
      orderMaterial={orderMaterial}
      declared={list}
      onClose={jest.fn()}
    />,
  );
  fireEvent.mouseDown(screen.getByRole('combobox'));
};
const optionTitle = (code: string) => document.querySelector(`[title^="${code} "]`);

describe('OrderMaterialFormModal', () => {
  it('thêm NVL ngoài thì không có NVL đã có trong đơn', () => {
    openOptions(null, [declared(1, 'VAI01')]);

    expect(optionTitle('VAI01')).toBeNull();
    expect(optionTitle('VAI02')).not.toBeNull();
    expect(optionTitle('VAI03')).not.toBeNull();
  });

  it('sửa một dòng thì giữ NVL của dòng đó, ẩn NVL của dòng khác', () => {
    const editing = declared(1, 'VAI01');
    openOptions(editing, [editing, declared(2, 'VAI02')]);

    expect(optionTitle('VAI01')).not.toBeNull();
    expect(optionTitle('VAI02')).toBeNull();
    expect(optionTitle('VAI03')).not.toBeNull();
  });
});
