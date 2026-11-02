import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { TeamListItem } from '../types';
import { TeamsPage } from './TeamsPage';

const team = (id: number, status: TeamListItem['status']) =>
  ({
    teamId: id,
    teamCode: `T${id}`,
    teamName: `Tổ ${id}`,
    teamType: 'Sewing',
    status,
    leaderName: null,
    deputyName: null,
    memberCount: 0,
  }) as TeamListItem;

jest.mock('../hooks/useTeams', () => ({
  useTeams: () => ({
    data: [team(1, 'Active'), team(2, 'Discontinued')],
    isFetching: false,
    error: null,
  }),
  useTeamStatus: () => ({ mutate: jest.fn() }),
}));
jest.mock('../components/TeamFormModal', () => ({ TeamFormModal: () => null }));
jest.mock('../components/TeamDetailModal', () => ({ TeamDetailModal: () => null }));

const renderPage = () =>
  render(
    <MemoryRouter>
      <TeamsPage />
    </MemoryRouter>,
  );

describe('TeamsPage', () => {
  it('mặc định hiện mọi tổ, lọc Ngừng thì chỉ còn tổ đã ngừng', async () => {
    renderPage();
    expect(screen.getByText('Tổ 1')).toBeInTheDocument();
    expect(screen.getByText('Tổ 2')).toBeInTheDocument();

    fireEvent.mouseDown(screen.getAllByRole('combobox')[0] as HTMLElement);
    const options = await screen.findAllByText('Ngừng');
    fireEvent.click(options[options.length - 1] as HTMLElement);

    expect(screen.queryByText('Tổ 1')).not.toBeInTheDocument();
    expect(screen.getByText('Tổ 2')).toBeInTheDocument();
  });
});
