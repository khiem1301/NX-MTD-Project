import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { PRODUCTION_TEXT } from '../constants';
import { DefectFormModal } from './DefectFormModal';

const mutate = jest.fn();

jest.mock('@/services/env', () => ({ env: { apiBaseUrl: 'http://localhost' } }));
jest.mock('@/hooks/useAuth', () => ({ useAuth: () => ({ user: { teamId: 5 } }) }));
jest.mock('@/hooks/useHrLookups', () => ({
  useTeamMemberLookup: () => ({
    isLoading: false,
    data: [{ employeeId: 7, employeeCode: 'CN7', fullName: 'Công nhân 7', role: 'Worker' }],
  }),
}));
jest.mock('@/hooks/useOrderLookups', () => ({
  useOrderLineLookup: () => ({ isLoading: false, data: [] }),
}));
jest.mock('@/hooks/useLookups', () => ({
  useOrderOptions: () => ({ isLoading: false, data: [] }),
  useWorkDateRange: () => () => false,
}));
jest.mock('../hooks/useAssignments', () => ({
  useAssignableOperations: () => ({ isLoading: false, data: [] }),
}));
jest.mock('../hooks/useDefects', () => ({
  useCreateDefect: () => ({ mutate, isPending: false }),
}));

const text = PRODUCTION_TEXT.defects.form;

const openModal = () => render(<DefectFormModal open onClose={jest.fn()} />);

const chooseType = async (label: string) => {
  const select = screen.getByLabelText(text.type);
  fireEvent.mouseDown(select);
  const option = await screen.findByTitle(label);
  fireEvent.click(option);
};

describe('DefectFormModal', () => {
  beforeEach(() => mutate.mockReset());

  it('mặc định là hàng sửa lại: có ô lý do hỏng, không có hướng dẫn nhập sai', () => {
    openModal();

    expect(screen.getByLabelText(text.reason)).toBeInTheDocument();
    expect(screen.queryByText(text.outputErrorHint)).not.toBeInTheDocument();
  });

  it('chọn Nhập sai sản lượng: ẩn lý do hỏng, hiện hướng dẫn và ghi chú bắt buộc', async () => {
    openModal();

    await chooseType(PRODUCTION_TEXT.defects.type.OutputErr.text);

    await waitFor(() => expect(screen.getByText(text.outputErrorHint)).toBeInTheDocument());
    expect(screen.queryByLabelText(text.reason)).not.toBeInTheDocument();
    expect(screen.getByLabelText(text.outputErrorNote)).toBeInTheDocument();
  });

  it('Nhập sai sản lượng thiếu ghi chú thì báo lỗi và không gọi API', async () => {
    openModal();
    await chooseType(PRODUCTION_TEXT.defects.type.OutputErr.text);

    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    expect(await screen.findByText(text.outputErrorNoteRequired)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });
});
