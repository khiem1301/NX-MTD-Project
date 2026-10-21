import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { apiClient } from '@/services/apiClient';
import { AttachmentUploadButton } from './AttachmentUploadButton';

jest.mock('@/services/apiClient', () => ({ apiClient: { post: jest.fn(), get: jest.fn(), delete: jest.fn() } }));

const renderButton = (remaining: number) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AttachmentUploadButton ownerType="Request" ownerId={1} remaining={remaining} />
    </QueryClientProvider>,
  );

describe('AttachmentUploadButton', () => {
  it('khóa nút khi đã đủ số ảnh', () => {
    renderButton(0);
    expect(screen.getByRole('button', { name: /Thêm ảnh/ })).toBeDisabled();
  });

  it('chặn ảnh lớn hơn 5 MB, không gọi API', async () => {
    const { container } = renderButton(3);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    const big = new File([new ArrayBuffer(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });

    fireEvent.change(input, { target: { files: [big] } });

    await waitFor(() => expect(apiClient.post).not.toHaveBeenCalled());
  });
});
