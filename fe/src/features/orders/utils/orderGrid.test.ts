import type { OrderLine } from '../types';
import {
  blockTotal,
  blocksTotals,
  fromLines,
  sortSizes,
  toLines,
  type ProductBlock,
} from './orderGrid';

const line = (
  id: number,
  color: string,
  size: string,
  qty: number,
  price = 12500,
  productId = 1,
): OrderLine => ({
  orderLineId: id,
  productId,
  productCode: 'AO-01',
  productName: 'Áo',
  color,
  sizeCode: size,
  orderedQty: qty,
  cmtUnitPrice: price,
});

describe('orderGrid', () => {
  it('fromLines → toLines trả lại đúng tập dòng', () => {
    const lines = [line(1, 'Đỏ', 'S', 10), line(2, 'Đỏ', 'M', 20), line(3, 'Xanh', 'M', 5)];

    const back = toLines(fromLines(lines));

    expect(back).toHaveLength(3);
    expect(
      back.map((l) => [l.orderLineId, l.color, l.sizeCode, l.orderedQty, l.cmtUnitPrice]).sort(),
    ).toEqual(
      lines.map((l) => [l.orderLineId, l.color, l.sizeCode, l.orderedQty, l.cmtUnitPrice]).sort(),
    );
  });

  it('cùng mã hàng nhưng khác giá thì tách thành 2 khối', () => {
    const blocks = fromLines([line(1, 'Đỏ', 'S', 1, 100), line(2, 'Đỏ', 'M', 1, 200)]);
    expect(blocks).toHaveLength(2);
  });

  it('sortSizes xếp theo thứ tự chuẩn, size lạ xếp sau', () => {
    expect(sortSizes(['XL', 'S', '4XL', 'M'])).toEqual(['S', 'M', 'XL', '4XL']);
  });

  it('ô trống / 0 và khối chưa chọn mã hàng không sinh dòng', () => {
    const blocks: ProductBlock[] = [
      {
        key: 'a',
        productId: 1,
        cmtUnitPrice: 10,
        sizes: ['S', 'M'],
        rows: [
          {
            key: 'r',
            color: 'Đỏ',
            cells: { S: { orderLineId: null, qty: 0 }, M: { orderLineId: null, qty: null } },
          },
        ],
      },
      {
        key: 'b',
        productId: null,
        cmtUnitPrice: 10,
        sizes: ['S'],
        rows: [{ key: 'r2', color: 'X', cells: { S: { orderLineId: null, qty: 5 } } }],
      },
    ];
    expect(toLines(blocks)).toEqual([]);
  });

  it('màu "Đỏ" và "đỏ" gộp một hàng', () => {
    const [block] = fromLines([line(1, 'Đỏ', 'S', 1), line(2, 'đỏ', 'M', 2)]);
    expect(block?.rows).toHaveLength(1);
  });

  it('tính tổng khối và tổng đơn', () => {
    const blocks = fromLines([line(1, 'Đỏ', 'S', 10, 100), line(2, 'Đỏ', 'M', 20, 100)]);
    expect(blockTotal(blocks[0] as ProductBlock)).toBe(30);
    expect(blocksTotals(blocks)).toEqual({ qty: 30, amount: 3000 });
  });
});
