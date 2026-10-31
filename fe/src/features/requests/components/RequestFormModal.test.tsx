import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { requestApi } from '../api/requestApi';
import { RequestFormModal } from './RequestFormModal';

jest.mock('../api/requestApi', () => ({
  requestApi: { create: jest.fn(), update: jest.fn(), types: jest.fn() },
}));
jest.mock('@/hooks/useAttachments', () => ({ attachmentApi: { upload: jest.fn() } }));

const TYPES = [
  'SpareParts',
  'NewMachine',
  'MachineSupplies',
  'MaterialRequisition',
  'AttendanceCorrection',
].map((t) => ({
  requestType: t,
  label: t,
  needsApproval: true,
  handlerRole: 'Admin',
}));

const renderModal = () => {
  (requestApi.types as jest.Mock).mockResolvedValue(TYPES);
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <RequestFormModal open maxPhotos={5} onClose={jest.fn()} onSaved={jest.fn()} />
    </QueryClientProvider>,
  );
};

describe('RequestFormModal', () => {
  beforeEach(() => (requestApi.create as jest.Mock).mockReset());

  it('chỉ liệt kê loại đơn dùng form chung (Cơ điện + Xin NVL / BTP), bỏ loại có form riêng', async () => {
    renderModal();

    // getByRole rất chậm trên DOM antd trong jsdom nên dùng class của rc-select.
    await waitFor(() => expect(requestApi.types).toHaveBeenCalled());
    fireEvent.mouseDown(document.querySelector('.ant-select-selector') as Element);

    await waitFor(() =>
      expect(document.querySelectorAll('.ant-select-item-option')).toHaveLength(4),
    );
  });

  it('thiếu nội dung thì báo lỗi và không gọi API tạo đơn', async () => {
    renderModal();

    fireEvent.click(screen.getByText('Lưu'));

    await waitFor(() => expect(document.querySelector('[aria-invalid="true"]')).not.toBeNull());
    expect(requestApi.create).not.toHaveBeenCalled();
  });
});
