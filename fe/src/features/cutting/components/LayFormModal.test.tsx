import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import type { WorkOrder } from '../types';
import { LayFormModal } from './LayFormModal';

const mockSave = jest.fn();

jest.mock('../hooks/useCutting', () => ({
  useSaveLay: () => ({ mutate: mockSave, isPending: false }),
}));

const fabric = (materialId: number, code: string, cutQty: number) => ({
  materialId,
  materialCode: code,
  materialName: code === 'V01' ? 'Kaki' : 'Lót',
  unit: 'm',
  materialColor: '',
  issuedQty: 100,
  availableQty: 0,
  receivedQty: 100,
  returnedQty: 0,
  usedQty: 100,
  heldQty: 100,
  cutQty,
  remainingQty: 100 - cutQty,
  unmeasuredLayCount: 0,
});

/** Áo Đen cả bộ dùng 2 vải: thân V01 (còn 40 m), lót V02 (còn 90 m). */
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
      fabricMaterialIds: [1, 2],
    },
  ],
  fabrics: [fabric(1, 'V01', 60), fabric(2, 'V02', 10)],
} as unknown as WorkOrder;

const renderModal = () =>
  render(<LayFormModal open workOrder={workOrder} lay={null} onClose={jest.fn()} />);

const usedInput = (code: string) => screen.getByLabelText(`${LABELS.layFabricUsed} ${code}`);
const qtyInput = () => screen.getByLabelText(`${LABELS.actualQty} Đen - M`);
const save = () => fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.save }));

describe('LayFormModal', () => {
  beforeEach(() => mockSave.mockReset());

  it('chưa nhập số lượng thì chưa hiện vải; cắt cả bộ thì hiện đủ mọi vải của bộ', async () => {
    renderModal();
    expect(screen.getByText(LABELS.layFabricsEmpty)).toBeInTheDocument();

    fireEvent.change(qtyInput(), { target: { value: '20' } });

    expect(await screen.findByLabelText(`${LABELS.layFabricUsed} V01`)).toBeInTheDocument();
    expect(usedInput('V02')).toBeInTheDocument();
  });

  it('bắt buộc nhập đủ số mét mọi vải', async () => {
    renderModal();
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    fireEvent.change(await screen.findByLabelText(`${LABELS.layFabricUsed} V01`), {
      target: { value: '30' },
    });
    save();

    expect(await screen.findByText(MESSAGES.common.required)).toBeInTheDocument();
    expect(mockSave).not.toHaveBeenCalled();
  });

  it('chặn vải vượt số còn lại của tổ', async () => {
    renderModal();
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    fireEvent.change(await screen.findByLabelText(`${LABELS.layFabricUsed} V01`), {
      target: { value: '41' },
    });
    fireEvent.change(usedInput('V02'), { target: { value: '5' } });
    save();

    expect(
      await screen.findByText(fill(MESSAGES.cutting.layFabricOverRemaining, '40')),
    ).toBeInTheDocument();
    expect(mockSave).not.toHaveBeenCalled();
  });

  it('gửi số lượng sau cắt và số mét từng vải', async () => {
    renderModal();
    fireEvent.change(qtyInput(), { target: { value: '20' } });
    fireEvent.change(await screen.findByLabelText(`${LABELS.layFabricUsed} V01`), {
      target: { value: '40' },
    });
    fireEvent.change(usedInput('V02'), { target: { value: '12,5' } });
    save();

    await waitFor(() => expect(mockSave).toHaveBeenCalled());
    expect(mockSave.mock.calls[0][0].body).toMatchObject({
      workOrderId: 7,
      fabrics: [
        { materialId: 1, usedM: 40 },
        { materialId: 2, usedM: 12.5 },
      ],
      lines: [{ orderLineId: 10, productComponentMaterialId: null, actualQty: 20 }],
    });
  });
});
