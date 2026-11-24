import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { useAttachments } from '@/hooks/useAttachments';
import { VL_LABELS, VL_MESSAGES } from '../constants';
import type { VehicleTrip } from '../types';
import { TripCompleteModal } from './TripCompleteModal';

const mutate = jest.fn();
jest.mock('../hooks/useVehicleLog', () => ({
  useCompleteTrip: () => ({ mutate, isPending: false }),
}));
jest.mock('@/hooks/useAttachments', () => ({
  useAttachments: jest.fn(),
  useAttachmentObjectUrl: () => 'blob:test',
  useRemoveAttachment: () => ({ mutate: jest.fn() }),
  useUploadAttachment: () => ({ mutate: jest.fn(), isPending: false }),
}));

const trip = { vehicleTripId: 5, plateNumber: '51C-123.45', odometerStart: 12388 } as VehicleTrip;

const withPhotos = (purposes: string[]) =>
  (useAttachments as jest.Mock).mockReturnValue({
    isLoading: false,
    isError: false,
    data: purposes.map((purpose, i) => ({ attachmentId: i + 1, purpose, fileName: 'a.jpg' })),
  });

const typeEnd = (value: string) =>
  fireEvent.change(screen.getByLabelText(VL_LABELS.odometerEnd), { target: { value } });
const typeFuelCost = (value: string) =>
  fireEvent.change(screen.getByLabelText(VL_LABELS.fuelCost), { target: { value } });
const save = () => fireEvent.click(screen.getByRole('button', { name: MESSAGES.common.save }));

describe('TripCompleteModal', () => {
  beforeEach(() => {
    mutate.mockReset();
    withPhotos([]);
  });

  it('km cuối nhỏ hơn km đầu: báo lỗi, không lưu', async () => {
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12000');
    save();

    expect(await screen.findByText('Km cuối phải lớn hơn km đầu (12388).')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('km cuối lớn hơn km đầu, không nhập tiền xăng: lưu được, không cần ảnh', async () => {
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12500');
    save();

    await waitFor(() => expect(mutate).toHaveBeenCalled());
    expect(mutate.mock.calls[0][0]).toMatchObject({ id: 5, body: { odometerEnd: 12500 } });
  });

  it('nhập tiền xăng mà chưa có ảnh hóa đơn: hiện khung tải ảnh, báo lỗi, không lưu', async () => {
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12500');
    typeFuelCost('100000');
    save();

    expect(await screen.findByText(VL_MESSAGES.fuelReceiptRequired)).toBeInTheDocument();
    expect(screen.getByText(VL_LABELS.receiptPhotos)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('nhập tiền xăng và đã có ảnh hóa đơn: lưu được', async () => {
    withPhotos(['FuelReceipt']);
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12500');
    typeFuelCost('100000');
    save();

    await waitFor(() => expect(mutate).toHaveBeenCalled());
    expect(mutate.mock.calls[0][0]).toMatchObject({
      body: { odometerEnd: 12500, fuelCost: 100000 },
    });
  });

  it('ảnh khác loại (không phải hóa đơn xăng) không được tính', async () => {
    withPhotos(['Photo']);
    render(<TripCompleteModal trip={trip} onClose={jest.fn()} />);
    typeEnd('12500');
    typeFuelCost('100000');
    save();

    expect(await screen.findByText(VL_MESSAGES.fuelReceiptRequired)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });
});
