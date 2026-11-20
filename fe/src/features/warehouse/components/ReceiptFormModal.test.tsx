import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ReceiptFormModal } from './ReceiptFormModal';

const upload = jest.fn().mockResolvedValue({});
const mutate = jest.fn();

jest.mock('@/hooks/useAttachments', () => ({
  attachmentApi: { upload: (...args: unknown[]) => upload(...args) },
}));
jest.mock('../hooks/useReceipts', () => ({
  useSaveReceipt: () => ({ mutate, isPending: false }),
}));
// Dữ liệu trả về phải cùng một đối tượng giữa các lần render, nếu không effect điền dòng NVL chạy lại mãi.
const mockOrderMaterials = {
  data: [
    {
      orderMaterialId: 5,
      orderId: 1,
      materialCode: 'V1',
      materialName: 'Vải',
      materialColor: null,
      unit: 'm',
      customerQty: 10,
      receivedQty: 0,
    },
  ],
};
const mockPurchaseOrders = { data: { items: [] } };
const mockNoPurchaseOrder = { data: undefined };
jest.mock('../hooks/useOrderMaterials', () => ({ useOrderMaterials: () => mockOrderMaterials }));
jest.mock('../hooks/usePurchaseOrders', () => ({
  usePurchaseOrders: () => mockPurchaseOrders,
  usePurchaseOrder: () => mockNoPurchaseOrder,
}));
jest.mock('@/components/common/OrderSelect', () => ({ OrderSelect: () => null }));

describe('ReceiptFormModal: ảnh chứng từ khi lập phiếu mới', () => {
  beforeAll(() => {
    // jsdom không có hai hàm này mà ô tải ảnh dùng để xem trước.
    window.URL.createObjectURL = jest.fn(() => 'blob:preview');
    window.URL.revokeObjectURL = jest.fn();
  });

  it('tải ảnh đã chọn lên ngay sau khi phiếu được tạo', async () => {
    const onClose = jest.fn();
    mutate.mockImplementation((_vars: unknown, opts: { onSuccess: (r: unknown) => void }) =>
      opts.onSuccess({ materialReceiptId: 9 }),
    );
    const { container } = render(
      <ReceiptFormModal
        open
        receipt={null}
        preset={{ source: 'CUSTOMER', orderId: 1, orderMaterialId: 5, acceptedQty: 3 }}
        onClose={onClose}
      />,
    );
    const input = container.ownerDocument.querySelector('input[type=file]') as HTMLInputElement;
    const file = new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 1])], 'a.jpg', {
      type: 'image/jpeg',
    });
    fireEvent.change(input, { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(mutate).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(upload).toHaveBeenCalledTimes(1));
    expect(upload.mock.calls[0]?.[1]).toBe(9);
    expect(upload.mock.calls[0]?.[2]).toBeInstanceOf(File);
  });
});
