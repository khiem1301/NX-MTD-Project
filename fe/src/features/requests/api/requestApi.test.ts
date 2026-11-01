import { apiClient } from '@/services/apiClient';
import { requestApi } from './requestApi';

jest.mock('@/services/apiClient', () => ({
  apiClient: { post: jest.fn(), get: jest.fn(), put: jest.fn() },
}));

const post = apiClient.post as jest.Mock;

describe('requestApi.act', () => {
  beforeEach(() => post.mockReset());

  it('cancel gọi /requests/{id}/cancel và không gửi kind, id trong body', async () => {
    await requestApi.act({ kind: 'cancel', id: 5, reason: 'nhầm', rowVer: 'AQID' });
    expect(post).toHaveBeenCalledWith('/api/v1/common/requests/5/cancel', {
      reason: 'nhầm',
      rowVer: 'AQID',
    });
  });

  it('approve gọi /request-inbox/{id}/approve', async () => {
    await requestApi.act({ kind: 'approve', id: 5, note: null, rowVer: 'AQID' });
    expect(post).toHaveBeenCalledWith('/api/v1/common/request-inbox/5/approve', {
      note: null,
      rowVer: 'AQID',
    });
  });

  it('reject gọi /request-inbox/{id}/reject kèm lý do', async () => {
    await requestApi.act({ kind: 'reject', id: 5, reason: 'x', rowVer: 'AQID' });
    expect(post).toHaveBeenCalledWith('/api/v1/common/request-inbox/5/reject', {
      reason: 'x',
      rowVer: 'AQID',
    });
  });
});
