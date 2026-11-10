import type { OrderLine } from '../types';
import {
  blockTotal,
  blocksTotals,
  cloneBlocksForNewOrder,
  copyGridRow,
  fromLines,
  linesDelivered,
  rowDelivered,
  rowTotal,
  sizeTotal,
  sortSizes,
  toLines,
  type GridRow,
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

describe('fromLines không có đơn giá (Quản đốc)', () => {
  it('gom theo mã hàng, đơn giá null, thành tiền 0', () => {
    const line = {
      orderLineId: 1,
      productId: 1,
      productCode: 'AO-01',
      productName: 'Áo',
      color: 'Đỏ',
      orderedQty: 10,
    };
    const blocks = fromLines([
      { ...line, sizeCode: 'S' },
      { ...line, orderLineId: 2, sizeCode: 'M' },
    ]);

    expect(blocks).toHaveLength(1);
    expect(blocks[0]?.cmtUnitPrice).toBeNull();
    expect(blocksTotals(blocks)).toEqual({ qty: 20, amount: 0 });
  });
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

  it('tính tổng theo màu và theo size, ô trống tính 0', () => {
    const block = fromLines([
      line(1, 'Đỏ', 'S', 10),
      line(2, 'Đỏ', 'M', 20),
      line(3, 'Xanh', 'S', 5),
    ])[0] as ProductBlock;
    expect(rowTotal(block, block.rows[1] as GridRow)).toBe(5);
    expect(sizeTotal(block, 'S')).toBe(15);
    expect(sizeTotal(block, 'M')).toBe(20);
  });
});

describe('số đã giao của đơn', () => {
  const delivered = (
    id: number,
    color: string,
    size: string,
    qty: number,
    done?: number,
  ): OrderLine => ({
    ...line(id, color, size, qty),
    deliveredQty: done,
  });

  it('cộng số đã giao theo màu (mọi size) và theo cả đơn', () => {
    const lines = [
      delivered(1, 'Đỏ', 'S', 100, 40),
      delivered(2, 'Đỏ', 'M', 100, 60),
      delivered(3, 'Xanh', 'S', 50, 0),
    ];
    const [block] = fromLines(lines);
    expect(block).toBeDefined();
    const red = block?.rows.find((r) => r.color === 'Đỏ');
    expect(block && red && rowDelivered(block, red)).toBe(100);
    expect(linesDelivered(lines)).toBe(100);
  });

  it('dòng chưa có số đã giao tính là 0', () => {
    expect(linesDelivered([delivered(1, 'Đỏ', 'S', 10)])).toBe(0);
  });
});

describe('copyGridRow', () => {
  it('giữ số lượng, bỏ màu và orderLineId', () => {
    const source: GridRow = {
      key: 'r1',
      color: 'Cam',
      cells: {
        S: { orderLineId: 11, qty: 10, deliveredQty: 2 },
        M: { orderLineId: 12, qty: 20 },
      },
    };
    const copied = copyGridRow(source);
    expect(copied.key).not.toBe(source.key);
    expect(copied.color).toBe('');
    expect(copied.cells.S).toEqual({ orderLineId: null, qty: 10 });
    expect(copied.cells.M).toEqual({ orderLineId: null, qty: 20 });
  });
});

describe('cloneBlocksForNewOrder', () => {
  it('giữ màu / size / số lượng, bỏ orderLineId để tạo đơn mới', () => {
    const [block] = cloneBlocksForNewOrder([
      line(1, 'Đỏ', 'S', 10),
      line(2, 'Đỏ', 'M', 20),
      line(3, 'Xanh', 'S', 5),
    ]);
    expect(block?.rows).toHaveLength(2);
    expect(block?.rows[0]?.color).toBe('Đỏ');
    expect(block?.rows[0]?.cells.S).toEqual({ orderLineId: null, qty: 10 });
    expect(block?.rows[0]?.cells.M).toEqual({ orderLineId: null, qty: 20 });
    expect(block?.rows[1]?.color).toBe('Xanh');
    expect(block?.rows[1]?.cells.S).toEqual({ orderLineId: null, qty: 5 });
  });
});
