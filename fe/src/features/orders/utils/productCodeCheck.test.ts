import { productApi } from '../api/orderApi';
import { productCodeExists } from './productCodeCheck';

jest.mock('../api/orderApi', () => ({ productApi: { list: jest.fn() } }));

const list = productApi.list as jest.Mock;

describe('productCodeExists', () => {
  beforeEach(() => list.mockReset());

  it('khớp chính xác không phân biệt hoa thường', async () => {
    list.mockResolvedValue({ items: [{ productCode: 'AO-01' }, { productCode: 'AO-011' }] });
    await expect(productCodeExists(' ao-01 ')).resolves.toBe(true);
  });

  it('mã gần giống nhưng khác thì chưa có', async () => {
    list.mockResolvedValue({ items: [{ productCode: 'AO-011' }] });
    await expect(productCodeExists('AO-01')).resolves.toBe(false);
  });

  it('không kiểm được thì không chặn', async () => {
    list.mockRejectedValue(new Error('network'));
    await expect(productCodeExists('AO-01')).resolves.toBe(false);
  });

  it('mã trống không gọi máy chủ', async () => {
    await expect(productCodeExists('  ')).resolves.toBe(false);
    expect(list).not.toHaveBeenCalled();
  });
});
