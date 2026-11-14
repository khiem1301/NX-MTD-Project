import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { PRODUCTION_TEXT } from '../constants';
import type { Defect } from '../types';
import { DefectEditModal } from './DefectEditModal';

const mutate = jest.fn();

jest.mock('@/services/env', () => ({ env: { apiBaseUrl: 'http://localhost' } }));
jest.mock('@/hooks/useHrLookups', () => ({
  useTeamMemberLookup: () => ({
    isLoading: false,
    data: [{ employeeId: 7, employeeCode: 'CN7', fullName: 'Công nhân 7', role: 'Worker' }],
  }),
}));
jest.mock('../hooks/useAssignments', () => ({
  useAssignableOperations: () => ({
    isLoading: false,
    data: [{ operationId: 1, productCode: 'SP1', operationCode: 'OP10', operationName: 'May cổ' }],
  }),
}));
jest.mock('../hooks/useDefects', () => ({
  useUpdateDefect: () => ({ mutate, isPending: false }),
}));

const text = PRODUCTION_TEXT.defects;

const defect = {
  defectRecordId: 3,
  defectCode: 'MAY1-261021-001',
  shiftCode: 'MORNING',
  orderId: 1,
  sewingTeamId: 5,
  operationId: 1,
  responsibleWorkerId: 7,
  defectType: 'Scrap',
  quantity: 2,
  defectReasonCode: 'Torn',
  reasonNote: null,
  rowVer: 'AAA=',
} as unknown as Defect;

describe('DefectEditModal', () => {
  beforeEach(() => mutate.mockReset());

  it('điền sẵn dữ liệu phiếu và gửi bản sửa kèm rowVer', async () => {
    render(<DefectEditModal role="team-leader" defect={defect} onClose={jest.fn()} />);

    const qty = await screen.findByLabelText(text.form.qty);
    fireEvent.change(qty, { target: { value: '5' } });
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(mutate).toHaveBeenCalledTimes(1));
    expect(mutate.mock.calls[0]?.[0]).toMatchObject({
      id: 3,
      body: {
        quantity: 5,
        operationId: 1,
        responsibleWorkerId: 7,
        defectType: 'Scrap',
        rowVer: 'AAA=',
      },
    });
  });

  it('Quản đốc sửa thiếu lý do thì báo lỗi và không gọi API', async () => {
    render(<DefectEditModal role="manager" defect={defect} onClose={jest.fn()} />);

    fireEvent.click(await screen.findByRole('button', { name: 'Lưu' }));

    expect(await screen.findByText(text.editReasonRequired)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('không mở khi chưa chọn phiếu', () => {
    render(<DefectEditModal role="team-leader" defect={null} onClose={jest.fn()} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
