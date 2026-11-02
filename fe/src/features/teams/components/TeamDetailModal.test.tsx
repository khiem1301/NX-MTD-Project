import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { Modal } from 'antd';
import { MemoryRouter } from 'react-router-dom';
import { MESSAGES } from '@/constants/messages';
import type { TeamDetail, TeamMember } from '../types';
import { TeamDetailModal } from './TeamDetailModal';

const member = (id: number, role: TeamMember['role'], name: string): TeamMember => ({
  employeeId: id,
  employeeCode: `E${id}`,
  fullName: name,
  role,
  roleName: role,
  payType: 'PieceRate',
  currentGradeCode: null,
});

const detail = (status: TeamDetail['status'] = 'Active'): TeamDetail => ({
  teamId: 4,
  teamCode: 'TM1',
  teamName: 'Tổ may 1',
  teamType: 'Sewing',
  status,
  members: [member(1, 'TeamLeader', 'An'), member(2, 'Worker', 'Bình'), member(3, 'Worker', 'Chi')],
  history: [],
});

const removeMutate = jest.fn();
const dismissMutate = jest.fn();
let current = detail();

jest.mock('../hooks/useTeams', () => ({
  useTeam: () => ({ data: current, isLoading: false, error: null, refetch: jest.fn() }),
  useRemoveMembers: () => ({ mutate: removeMutate }),
  useDismiss: () => ({ mutate: dismissMutate }),
  useAppoint: () => ({ mutate: jest.fn() }),
}));
jest.mock('./AddMembersModal', () => ({ AddMembersModal: () => null }));
jest.mock('./TransferModal', () => ({ TransferModal: () => null }));

/** Nút xác nhận của đúng hộp thoại có nội dung này (hộp cũ đóng có hiệu ứng nên còn sót trong DOM). */
const confirmButton = async (text: RegExp) => {
  const [title] = await screen.findAllByText(text);
  const dialog = (title as HTMLElement).closest<HTMLElement>('.ant-modal-confirm');
  return within(dialog as HTMLElement).getByRole('button', { name: MESSAGES.common.confirm });
};

const renderModal = () =>
  render(
    <MemoryRouter initialEntries={['/?id=4']}>
      <TeamDetailModal />
    </MemoryRouter>,
  );

describe('TeamDetailModal', () => {
  beforeEach(() => {
    current = detail();
    jest.clearAllMocks();
  });
  afterEach(() => Modal.destroyAll());

  it('hiện tổ trưởng, tổ phó chưa có và số người', () => {
    renderModal();
    expect(screen.getByText(`${MESSAGES.hr.leader}: An`)).toBeInTheDocument();
    expect(screen.getByText(`${MESSAGES.hr.deputy}: ${MESSAGES.hr.noLeader}`)).toBeInTheDocument();
  });

  it('chưa chọn ai thì không loại / chuyển được; tổ trưởng không chọn được', () => {
    renderModal();
    expect(screen.getByRole('button', { name: MESSAGES.hr.removeMembers })).toBeDisabled();
    expect(screen.getByRole('button', { name: MESSAGES.hr.transfer })).toBeDisabled();
    const boxes = screen.getAllByRole('checkbox');
    expect(boxes[1]).toBeDisabled();
    expect(boxes[2]).toBeEnabled();
  });

  it('chọn công nhân rồi loại khỏi tổ sau khi xác nhận', async () => {
    renderModal();
    fireEvent.click(screen.getAllByRole('checkbox')[2] as HTMLElement);
    fireEvent.click(screen.getByRole('button', { name: MESSAGES.hr.removeMembers }));
    fireEvent.click(await confirmButton(/Loại 1 nhân viên/));
    await waitFor(() =>
      expect(removeMutate).toHaveBeenCalledWith(
        { id: 4, body: { employeeIds: [2] } },
        expect.anything(),
      ),
    );
  });

  it('thôi giữ chức tổ trưởng sau khi xác nhận', async () => {
    renderModal();
    fireEvent.click(screen.getByRole('button', { name: MESSAGES.hr.dismiss }));
    fireEvent.click(await confirmButton(/An sẽ chuyển về Công nhân/));
    await waitFor(() => expect(dismissMutate).toHaveBeenCalledWith({ id: 4, employeeId: 1 }));
  });

  it('tổ đã ngừng thì không thêm hay chuyển người được', () => {
    current = detail('Discontinued');
    renderModal();
    expect(screen.getByRole('button', { name: MESSAGES.hr.addMembers })).toBeDisabled();
  });
});
