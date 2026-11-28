import { fireEvent, render, screen } from '@testing-library/react';
import { fill, formatQty } from '@/utils/format';
import { VL_LABELS } from '../constants';
import { TripStartModal } from './TripModals';

jest.mock('../hooks/useVehicleLog', () => ({
  useStartTrip: () => ({ mutate: jest.fn(), isPending: false }),
  useVehicles: () => ({
    data: {
      items: [
        { vehicleId: 3, plateNumber: '51D-678.90', vehicleType: 'Xe van', lastOdometer: 11846 },
      ],
    },
    isLoading: false,
  }),
}));

describe('TripStartModal', () => {
  it('chọn xe: km cuối chuyến trước là gợi ý ngoài ô, ô để trống', async () => {
    render(<TripStartModal open onClose={jest.fn()} />);

    fireEvent.mouseDown(screen.getByRole('combobox'));
    fireEvent.click(await screen.findByText('51D-678.90 · Xe van'));

    expect(
      await screen.findByText(fill(VL_LABELS.odometerStartHint, formatQty(11846))),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(VL_LABELS.odometerStart)).toHaveValue('');
  });
});
