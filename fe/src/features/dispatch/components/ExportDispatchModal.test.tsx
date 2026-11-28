import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { fill, formatQty } from '@/utils/format';
import { DELIVERY_METHOD_LABEL, DISPATCH_LABELS } from '../constants';
import type { DispatchNote } from '../types';
import { ExportDispatchModal } from './ExportDispatchModal';

const mutate = jest.fn();

jest.mock('../hooks/useDispatch', () => ({
  useExportDispatch: () => ({ mutate, isPending: false }),
  useAvailableVehicles: () => ({
    data: [{ vehicleId: 3, plateNumber: '29C-12345', vehicleType: 'Xe tải', lastOdometer: 1200 }],
    isFetching: false,
  }),
}));

const note = { dispatchNoteId: 8, dispatchCode: 'XH-1', rowVer: 'AAA=' } as DispatchNote;

describe('ExportDispatchModal', () => {
  beforeEach(() => mutate.mockReset());

  it('khách tự lấy: gửi không kèm xe', async () => {
    render(<ExportDispatchModal note={note} onClose={jest.fn()} onDone={jest.fn()} />);

    fireEvent.click(screen.getByRole('button', { name: DISPATCH_LABELS.export }));

    await waitFor(() => expect(mutate).toHaveBeenCalled());
    expect(mutate.mock.calls[0][0]).toEqual({
      id: 8,
      body: {
        rowVer: 'AAA=',
        deliveryMethod: 'CUSTOMER_PICKUP',
        vehicleId: null,
        driverName: null,
        odometerStart: null,
      },
    });
  });

  it('chọn xe: km cuối chuyến trước là gợi ý ngoài ô, ô để trống', async () => {
    render(<ExportDispatchModal note={note} onClose={jest.fn()} onDone={jest.fn()} />);

    fireEvent.click(screen.getByText(DELIVERY_METHOD_LABEL.COMPANY_VEHICLE));
    fireEvent.mouseDown(await screen.findByRole('combobox'));
    fireEvent.click(await screen.findByText('29C-12345 · Xe tải'));

    expect(
      await screen.findByText(fill(DISPATCH_LABELS.odometerHint, formatQty(1200))),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(DISPATCH_LABELS.odometerStart)).toHaveValue('');
  });

  it('xe công ty: hiện ô xe, tài xế, km và bắt buộc nhập', async () => {
    render(<ExportDispatchModal note={note} onClose={jest.fn()} onDone={jest.fn()} />);

    fireEvent.click(screen.getByText(DELIVERY_METHOD_LABEL.COMPANY_VEHICLE));
    expect(await screen.findByText(DISPATCH_LABELS.vehicle)).toBeInTheDocument();
    expect(screen.getByText(DISPATCH_LABELS.driver)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: DISPATCH_LABELS.export }));

    expect(await screen.findByText(DISPATCH_LABELS.vehicleRequired)).toBeInTheDocument();
    expect(screen.getByText(DISPATCH_LABELS.driverRequired)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });
});
