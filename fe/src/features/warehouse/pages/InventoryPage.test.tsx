import { fireEvent, render, screen } from '@testing-library/react';
import { LABELS } from '../constants';
import { InventoryPage } from './InventoryPage';

jest.mock('../components/BalanceTable', () => ({ BalanceTable: () => <span>bảng tồn</span> }));
jest.mock('../components/TeamHoldingsTable', () => ({ TeamHoldingsTable: () => null }));
jest.mock('../components/NxtReportPanel', () => ({
  NxtReportPanel: () => <span>báo cáo nhập xuất tồn</span>,
}));

describe('InventoryPage', () => {
  it('báo cáo nhập xuất tồn là một tab của màn Tồn kho', () => {
    render(<InventoryPage />);

    expect(screen.queryByText('báo cáo nhập xuất tồn')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: LABELS.nxtReportTitle }));

    expect(screen.getByText('báo cáo nhập xuất tồn')).toBeInTheDocument();
  });
});
