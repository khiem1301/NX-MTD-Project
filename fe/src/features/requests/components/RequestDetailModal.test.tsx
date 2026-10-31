import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { requestApi } from '../api/requestApi';
import type { RequestDetail, RequestPermissions } from '../types';
import { RequestDetailModal } from './RequestDetailModal';

jest.mock('../api/requestApi', () => ({
  requestApi: { detail: jest.fn(), act: jest.fn(), types: jest.fn().mockResolvedValue([]) },
}));
jest.mock('@/hooks/useAttachments', () => ({ attachmentApi: { upload: jest.fn() } }));
jest.mock('@/components/common/attachments', () => ({
  AttachmentGallery: () => <div>gallery</div>,
}));

const NO_PERMISSIONS: RequestPermissions = {
  canEdit: false,
  canCancel: false,
  canApprove: false,
  canReject: false,
  canComplete: false,
};

const detail = (permissions: Partial<RequestPermissions>): RequestDetail => ({
  requestId: 1,
  requestCode: 'YC261022-001',
  requestType: 'SpareParts',
  status: 'PendingApproval',
  title: 'Thay chân vịt',
  content: 'Chân vịt gãy',
  targetDate: null,
  orderId: null,
  orderCode: null,
  createdBy: 7,
  createdByName: 'Cơ điện',
  createdByTeamId: null,
  teamName: null,
  createdAt: '2026-10-22T08:00:00+07:00',
  decidedAt: null,
  decisionNote: null,
  resultNote: null,
  completedAt: null,
  shortageResolution: null,
  rowVer: 'AQID',
  lines: [],
  history: [],
  permissions: { ...NO_PERMISSIONS, ...permissions },
  maxPhotos: 5,
});

const renderDrawer = (d: RequestDetail) => {
  (requestApi.detail as jest.Mock).mockResolvedValue(d);
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <MemoryRouter initialEntries={['/requests?id=1']}>
        <RequestDetailModal />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('RequestDetailModal', () => {
  beforeEach(() => (requestApi.act as jest.Mock).mockReset().mockResolvedValue(undefined));

  it('không hiện nút Từ chối khi server không cấp quyền', async () => {
    renderDrawer(detail({ canApprove: true }));

    expect(await screen.findByText('Duyệt')).toBeInTheDocument();
    expect(screen.queryByText('Từ chối')).not.toBeInTheDocument();
  });

  it('từ chối để trống lý do thì báo MSG09 và không gọi API', async () => {
    renderDrawer(detail({ canReject: true }));

    fireEvent.click(await screen.findByText('Từ chối'));
    fireEvent.click(await screen.findByText('Xác nhận'));

    expect(await screen.findByText('Vui lòng nhập lý do.')).toBeInTheDocument();
    expect(requestApi.act).not.toHaveBeenCalled();
  });

  it('từ chối có lý do thì gọi act kèm rowVer', async () => {
    renderDrawer(detail({ canReject: true }));

    fireEvent.click(await screen.findByText('Từ chối'));
    fireEvent.change(await screen.findByLabelText('Lý do từ chối'), {
      target: { value: 'Dùng đồ dự phòng' },
    });
    fireEvent.click(screen.getByText('Xác nhận'));

    await waitFor(() =>
      expect(requestApi.act).toHaveBeenCalledWith({
        kind: 'reject',
        id: 1,
        reason: 'Dùng đồ dự phòng',
        rowVer: 'AQID',
      }),
    );
  });
});
