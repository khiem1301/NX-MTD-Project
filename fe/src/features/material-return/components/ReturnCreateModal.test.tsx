import { fireEvent, render, screen, within } from '@testing-library/react';
import { RETURN_LABELS } from '../constants';
import type { ReturnableLine } from '../types';
import { ReturnCreateModal } from './ReturnCreateModal';

const line = (id: number, suggestedQty: number | null): ReturnableLine => ({
  materialIssueId: 10,
  issueCode: 'PC-1',
  materialIssueLineId: id,
  orderCode: 'DH-1',
  materialCode: 'V01',
  materialName: 'Vải gió',
  unit: 'm',
  materialColor: 'Đen',
  receivedQty: 100,
  returnedQty: 0,
  remainingQty: 100,
  suggestedQty,
});

let mockLines: ReturnableLine[] = [];
jest.mock('../hooks/useMaterialReturn', () => ({
  useReturnable: () => ({ data: mockLines, isLoading: false }),
  useCreateReturn: () => ({ mutate: jest.fn(), isPending: false }),
}));

const pickIssue = async () => {
  render(<ReturnCreateModal open onClose={jest.fn()} />);
  fireEvent.mouseDown(screen.getAllByRole('combobox')[0] as HTMLElement);
  fireEvent.click(await screen.findByTitle('PC-1 · DH-1'));
};

describe('ReturnCreateModal', () => {
  it('chọn phiếu cấp thì điền sẵn số vải còn lại sau cắt', async () => {
    mockLines = [line(100, 40)];
    await pickIssue();

    expect(await screen.findByText(RETURN_LABELS.suggestHint)).toBeInTheDocument();
    const row = screen.getByText(/V01 Vải gió/).closest('tr') as HTMLElement;
    expect(within(row).getByRole('spinbutton')).toHaveValue('40');
  });

  it('chưa tính được vải còn lại thì để trống và báo nhập theo cuộn thực tế', async () => {
    mockLines = [line(100, null)];
    await pickIssue();

    expect(await screen.findByText(RETURN_LABELS.noSuggestHint)).toBeInTheDocument();
    const row = screen.getByText(/V01 Vải gió/).closest('tr') as HTMLElement;
    expect(within(row).getByRole('spinbutton')).toHaveValue('');
  });
});
