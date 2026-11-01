import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiCalendar } from '../types';
import { KpiCalendarBody } from './KpiCalendarBody';

jest.mock('../api/kpiApi', () => ({ kpiApi: { saveDailyPlan: jest.fn() } }));

dayjs.locale('vi');

const text = KPI_TEXT.calendar;

const DATA: KpiCalendar = {
  kpiTargetId: 7,
  orderCode: 'DH001',
  productCode: 'MH001',
  productName: 'Áo',
  sewingTeamName: 'Tổ may 1',
  startDate: '2026-10-14',
  endDate: '2026-10-17',
  today: '2026-10-15',
  dueDate: '2026-10-20',
  firstEditableDate: '2026-10-15',
  editCutoff: '08:00:00',
  targetFinishedQty: 1000,
  finishedQty: 30,
  canEdit: true,
  btp: { warehouseBtp: 80, teamHeld: 40, neededByOthers: 20, available: 100 },
  days: [
    { workDate: '2026-10-14', targetQty: 50, actualQty: 30 },
    { workDate: '2026-10-15', targetQty: 0, actualQty: 0 },
    { workDate: '2026-10-16', targetQty: 40, actualQty: null },
    { workDate: '2026-10-17', targetQty: 0, actualQty: null },
  ],
};

const renderBody = (editable = true, data = DATA) =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <KpiCalendarBody data={data} editable={editable} />
    </QueryClientProvider>,
  );

const dayInput = (date: string) => screen.getByLabelText(`KPI ngày ${date}`);
const type = (date: string, value: string) =>
  fireEvent.change(dayInput(date), { target: { value } });
const saveButton = () => screen.getByRole('button', { name: text.save });

describe('KpiCalendarBody', () => {
  beforeEach(() => jest.clearAllMocks());

  it('chỉ cho nhập ngày từ hôm nay trở đi, ngày đã qua hiện số đã đặt và thực tế', () => {
    renderBody();

    expect(screen.queryByLabelText('KPI ngày 2026-10-14')).not.toBeInTheDocument();
    expect(dayInput('2026-10-15')).toBeInTheDocument();
    expect(dayInput('2026-10-16')).toHaveValue('40');
    expect(screen.getByText(`${text.actual}: 30`, { exact: false })).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('sau giờ khóa thì hôm nay cũng không nhập được nữa', () => {
    renderBody(true, { ...DATA, firstEditableDate: '2026-10-16' });

    expect(screen.queryByLabelText('KPI ngày 2026-10-15')).not.toBeInTheDocument();
    expect(dayInput('2026-10-16')).toBeInTheDocument();
    expect(screen.getByText(/chỉ sửa trước 08:00/)).toBeInTheDocument();
  });

  it('lưu toàn bộ các ngày có KPI, gồm cả ngày đã qua', async () => {
    (kpiApi.saveDailyPlan as jest.Mock).mockResolvedValue(DATA);
    renderBody();

    type('2026-10-15', '40');
    fireEvent.click(saveButton());

    await waitFor(() =>
      expect(kpiApi.saveDailyPlan).toHaveBeenCalledWith(7, {
        dailyTargets: [
          { workDate: '2026-10-14', targetQty: 50 },
          { workDate: '2026-10-15', targetQty: 40 },
          { workDate: '2026-10-16', targetQty: 40 },
        ],
      }),
    );
  });

  it('chặn lưu khi KPI còn phải làm vượt BTP khả dụng', () => {
    renderBody();

    type('2026-10-16', '81'); // đã đặt 131, đã làm 30: còn phải làm 101 > BTP 100

    expect(screen.getByText(text.overBtp)).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();

    type('2026-10-16', '80');

    expect(screen.queryByText(text.overBtp)).not.toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  it('vẫn cho giảm KPI khi BTP đã thiếu từ trước', () => {
    renderBody(true, { ...DATA, btp: { ...DATA.btp, available: 10 } });

    type('2026-10-16', '30');

    expect(screen.queryByText(text.overBtp)).not.toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  it('chặn lưu khi tổng KPI các ngày vượt chỉ tiêu cả kỳ', () => {
    renderBody(true, { ...DATA, btp: { ...DATA.btp, available: 5000 } });

    type('2026-10-16', '951');

    expect(screen.getByText(text.overTotal)).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('hiện ngày giao hàng và đánh dấu trên ô ngày giao', () => {
    renderBody(true, { ...DATA, dueDate: '2026-10-16' });

    expect(screen.getByText(text.dueDate)).toBeInTheDocument();
    expect(screen.getByText('16/10/2026')).toBeInTheDocument();
    expect(screen.getByText(text.dueDay)).toBeInTheDocument();
  });

  it('cảnh báo khi đặt KPI sau ngày giao nhưng vẫn cho lưu', () => {
    renderBody(true, { ...DATA, dueDate: '2026-10-15', btp: { ...DATA.btp, available: 500 } });

    expect(
      screen.getByText(/Có 1 ngày đặt KPI sau ngày giao hàng 15\/10\/2026/),
    ).toBeInTheDocument();

    type('2026-10-17', '30');

    expect(screen.getByText(/Có 2 ngày đặt KPI sau ngày giao hàng/)).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
  });

  it('không cảnh báo khi mọi KPI đều trước ngày giao', () => {
    renderBody();

    expect(screen.queryByText(/sau ngày giao hàng/)).not.toBeInTheDocument();
  });

  it('Admin và chỉ tiêu đã đóng chỉ xem', () => {
    renderBody(false);

    expect(screen.getByText(text.readOnly)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: text.save })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('KPI ngày 2026-10-16')).not.toBeInTheDocument();
  });
});
