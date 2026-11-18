import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { VL_LABELS } from '../constants';
import type { VehicleTrip } from '../types';
import { TripCompleteModal } from './TripCompleteModal';

const mutate = jest.fn();
jest.mock('../hooks/useVehicleLog', () => ({
  useCompleteTrip: () => ({ mutate, isPending: false }),
}));

const trip = { vehicleTripId: 5, plateNumber: '51C-123.45', odometerStart: 12388 } as VehicleTrip;

const typeEnd = (value: string) =>
  fireEvent.change(screen.getByLabelText(VL_LABELS.odometerEnd), { target: { value } });

describe('TripCompleteModal', () => {
  beforeEach(() => mutate.mockReset());

  it('km cuối nhỏ hơn km đầu: báo lỗi, không lưu', async () => {
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12000');
    fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.save }));

    expect(await screen.findByText('Km cuối phải lớn hơn km đầu (12388).')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('km cuối lớn hơn km đầu: gửi lưu', async () => {
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12500');
    fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.save }));

    await waitFor(() => expect(mutate).toHaveBeenCalled());
    expect(mutate.mock.calls[0][0]).toMatchObject({ id: 5, body: { odometerEnd: 12500 } });
  });
});
