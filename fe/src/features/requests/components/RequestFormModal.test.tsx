import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { requestApi } from '@/services/requestApi';
import { RequestFormModal } from './RequestFormModal';

jest.mock('@/services/requestApi', () => ({
  requestApi: { create: jest.fn(), update: jest.fn(), types: jest.fn() },
}));
jest.mock('@/hooks/useAttachments', () => ({ attachmentApi: { upload: jest.fn() } }));
jest.mock('@/services/env', () => ({ env: { apiBaseUrl: 'http://localhost' } }));
jest.mock('@/services/lookupApi', () => ({
  lookupApi: { materials: jest.fn().mockResolvedValue([]) },
}));

const TYPES = [
  'SpareParts',
  'NewMachine',
  'MachineSupplies',
  'MaterialRequisition',
  'AttendanceCorrection',
  'Other',
].map((t) => ({
  requestType: t,
  label: t,
  needsApproval: true,
  handlerRole: t === 'Other' ? '' : 'Admin',
  handlerRoleOptions: t === 'Other' ? ['Accountant', 'TeamLeader'] : null,
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

// getByRole rất chậm trên DOM antd trong jsdom nên dùng class của rc-select.
const openTypeSelect = async () => {
  await waitFor(() => expect(requestApi.types).toHaveBeenCalled());
  fireEvent.mouseDown(document.querySelector('.ant-select-selector') as Element);
  await waitFor(() => expect(typeOptions().length).toBeGreaterThan(0));
};
const typeOptions = () => document.querySelectorAll('.ant-select-item-option');
const chooseOther = async () => {
  await openTypeSelect();
  fireEvent.click(document.querySelector('.ant-select-item-option[title="Other"]') as Element);
};

describe('RequestFormModal', () => {
  beforeEach(() => (requestApi.create as jest.Mock).mockReset());

  it('ô chọn loại đơn chỉ liệt kê loại dùng form chung', async () => {
    renderModal();
    await openTypeSelect();

    expect(typeOptions()).toHaveLength(5);
    expect(document.querySelector('[title="AttendanceCorrection"]')).toBeNull();
  });

  it('thiếu loại đơn và nội dung thì báo lỗi, không gọi API tạo đơn', async () => {
    renderModal();

    fireEvent.click(screen.getByText(MESSAGES.common.save));

    await waitFor(() => expect(document.querySelector('[aria-invalid="true"]')).not.toBeNull());
    expect(requestApi.create).not.toHaveBeenCalled();
  });

  it('đơn Khác: hiện ô người xử lý, không có dòng chi tiết', async () => {
    renderModal();
    await chooseOther();

    expect(await screen.findByText(MESSAGES.requests.handlerRole)).toBeInTheDocument();
    expect(screen.queryByText(MESSAGES.requests.addLine)).not.toBeInTheDocument();
  });

  it('đơn Khác thiếu người xử lý thì không gửi', async () => {
    renderModal();
    await chooseOther();
    await screen.findByText(MESSAGES.requests.handlerRole);
    fireEvent.change(screen.getByPlaceholderText(MESSAGES.requests.contentPlaceholder), {
      target: { value: 'Xin nghỉ chiều' },
    });

    fireEvent.click(screen.getByText(MESSAGES.common.save));

    expect(await screen.findByText(MESSAGES.common.required)).toBeInTheDocument();
    expect(requestApi.create).not.toHaveBeenCalled();
  });
});
