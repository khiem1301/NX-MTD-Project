import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import type { WorkOrder } from '../types';
import { LayFormModal } from './LayFormModal';

const mockSave = jest.fn();

jest.mock('../hooks/useCutting', () => ({
  useSaveLay: () => ({ mutate: mockSave, isPending: false }),
}));

const workOrder = {
  workOrderId: 7,
  workOrderCode: 'LC-7',
  createdAt: '2026-10-01T00:00:00Z',
  lines: [
    {
      workOrderLineId: 1,
      orderLineId: 10,
      color: 'Đen',
      sizeCode: 'M',
      productComponentMaterialId: null,
      componentName: '',
      plannedQty: 100,
      cutQty: 0,
      handedQty: 0,
    },
  ],
  fabrics: [
    {
      materialId: 1,
      materialCode: 'V01',
      materialName: 'Kaki',
      unit: 'm',
      materialColor: '',
      issuedQty: 100,
      availableQty: 0,
      receivedQty: 100,
      returnedQty: 0,
      usedQty: 100,
      heldQty: 100,
      cutQty: 60,
      remainingQty: 40,
      unmeasuredLayCount: 0,
    },
  ],
} as unknown as WorkOrder;

const renderModal = () =>
  render(<LayFormModal open workOrder={workOrder} lay={null} onClose={jest.fn()} />);

const fabricInput = () => screen.getByLabelText(LABELS.layFabricUsed);
const qtyInput = () => screen.getByLabelText(`${LABELS.actualQty} Đen - M`);
const save = () => fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.save }));

describe('LayFormModal', () => {
  beforeEach(() => mockSave.mockReset());

  it('hiện vải còn lại của tổ và tính lại khi nhập số mét', async () => {
    renderModal();
    fireEvent.change(fabricInput(), { target: { value: '15' } });

    const balance = screen.getByTestId('lay-fabric-balance');
    await waitFor(() => expect(within(balance).getByText('25')).toBeInTheDocument());
    expect(within(balance).getByText('40')).toBeInTheDocument();
  });

  it('chặn số mét vượt vải còn lại của tổ', async () => {
    renderModal();
    fireEvent.change(fabricInput(), { target: { value: '41' } });
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    save();

    expect(
      await screen.findByText(fill(MESSAGES.cutting.layFabricOverRemaining, '40')),
    ).toBeInTheDocument();
    expect(mockSave).not.toHaveBeenCalled();
  });

  it('bắt buộc nhập số mét vải đã dùng', async () => {
    renderModal();
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    save();

    expect(await screen.findByText(MESSAGES.common.required)).toBeInTheDocument();
    expect(mockSave).not.toHaveBeenCalled();
  });

  it('gửi số lượng sau cắt và số mét vải', async () => {
    renderModal();
    fireEvent.change(fabricInput(), { target: { value: '40' } });
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    save();

    await waitFor(() => expect(mockSave).toHaveBeenCalled());
    expect(mockSave.mock.calls[0][0].body).toMatchObject({
      workOrderId: 7,
      fabricMaterialId: 1,
      fabricUsedM: 40,
      lines: [{ orderLineId: 10, productComponentMaterialId: null, actualQty: 20 }],
    });
  });
});
