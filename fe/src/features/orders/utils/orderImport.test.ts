import type { OrderImportRow } from '../types';
import { newBlock, type ProductBlock } from './orderGrid';
import { acceptMissingProduct, mergeOrderImport } from './orderImport';
import type { OrderImportPreview } from '../types';

const row = (over: Partial<OrderImportRow>): OrderImportRow => ({
  rowNumber: 4,
  productCode: 'AO01',
  productId: 1,
  color: 'Đen',
  cmtUnitPrice: 25000,
  quantities: [],
  error: null,
  ...over,
});

const existing: ProductBlock = {
  key: 'b1',
  productId: 1,
  cmtUnitPrice: 20000,
  sizes: ['S', 'M'],
  rows: [
    {
      key: 'r1',
      color: 'Đen',
      cells: { S: { orderLineId: 11, qty: 5 }, M: { orderLineId: 12, qty: 7 } },
    },
  ],
};

describe('mergeOrderImport', () => {
  it('ghi đè size trong file của màu đã có, giữ size khác và orderLineId; thêm màu mới, đổi đơn giá', () => {
    const { blocks } = mergeOrderImport(
      [existing],
      [
        row({
          color: 'đen',
          quantities: [
            { sizeCode: 'S', qty: 100 },
            { sizeCode: 'L', qty: 3 },
          ],
        }),
        row({ color: 'Vàng', quantities: [{ sizeCode: 'M', qty: 9 }] }),
      ],
    );

    const block = blocks[0]!;
    expect(block.cmtUnitPrice).toBe(25000);
    expect(block.sizes).toEqual(['S', 'M', 'L']);
    expect(block.rows[0]!.cells).toEqual({
      S: { orderLineId: 11, qty: 100 },
      M: { orderLineId: 12, qty: 7 },
      L: { orderLineId: null, qty: 3 },
    });
    expect(block.rows[1]!.color).toBe('Vàng');
  });

  it('mã hàng chưa có thì thêm khối mới, bỏ khối trống và bỏ dòng lỗi', () => {
    const { blocks, labels } = mergeOrderImport(
      [newBlock()],
      [
        row({
          productId: 2,
          productCode: 'QU02',
          quantities: [{ sizeCode: 'M', qty: 4 }],
        }),
        row({ productId: null, error: 'Mã hàng không có', productCode: 'XX' }),
      ],
    );

    expect(blocks).toHaveLength(1);
    expect(blocks[0]!.productId).toBe(2);
    expect(labels).toEqual({ 2: 'QU02' });
  });
});

describe('acceptMissingProduct', () => {
  const preview: OrderImportPreview = {
    sizeColumns: ['M'],
    validCount: 0,
    errorCount: 2,
    duplicateCount: 0,
    rows: [
      row({
        productId: null,
        productCode: 'ao-moi',
        error: 'Mã hàng "ao-moi" không có trong danh mục.',
        missingProduct: true,
        quantities: [{ sizeCode: 'M', qty: 4 }],
      }),
      row({
        rowNumber: 5,
        productId: null,
        productCode: 'XX',
        error: 'Số lượng không hợp lệ',
        missingProduct: false,
      }),
    ],
  };

  it('gắn mã tạm cho dòng chỉ thiếu mã hàng, giữ dòng lỗi khác', () => {
    const next = acceptMissingProduct(preview, 'AO-MOI', -1);

    expect(next.rows[0]).toMatchObject({ productId: -1, error: null, missingProduct: false });
    expect(next.rows[1]!.productId).toBeNull();
    expect(next.validCount).toBe(1);
    expect(next.errorCount).toBe(1);
  });
});
