import type { ProductBlock } from './orderGrid';
import {
  colorErrorKey,
  orderCodeSchema,
  orderNoteSchema,
  qtyErrorKey,
  validateBlocks,
} from './orderValidation';

const valid: ProductBlock = {
  key: 'b1',
  productId: 1,
  cmtUnitPrice: 15000,
  sizes: ['S', 'M'],
  rows: [{ key: 'r1', color: 'Đỏ', cells: { S: { orderLineId: null, qty: 10 } } }],
};

const emptyBlock: ProductBlock = {
  key: 'empty',
  productId: null,
  cmtUnitPrice: null,
  sizes: ['S'],
  rows: [{ key: 'r0', color: '', cells: {} }],
};

const errorsOf = (block: ProductBlock, locked = false) =>
  validateBlocks([block], locked)[block.key];

describe('validateBlocks', () => {
  it('khối đủ thông tin thì không lỗi, đơn giá 0 vẫn hợp lệ', () => {
    expect(validateBlocks([valid], false)).toEqual({});
    expect(validateBlocks([{ ...valid, cmtUnitPrice: 0 }], false)).toEqual({});
  });

  it('bỏ qua khối trống hoàn toàn', () => {
    expect(validateBlocks([valid, emptyBlock], false)).toEqual({});
  });

  it('đã nhập số lượng nhưng chưa chọn mã hàng hoặc chưa nhập đơn giá thì báo lỗi', () => {
    const errors = errorsOf({ ...valid, productId: null, cmtUnitPrice: null });
    expect(errors?.product).toBe('Vui lòng chọn mã hàng.');
    expect(errors?.price).toBe('Vui lòng nhập đơn giá CMT.');
  });

  it('đơn giá vượt 100 triệu hoặc quá 2 số lẻ thì báo lỗi', () => {
    expect(errorsOf({ ...valid, cmtUnitPrice: 100_000_000.01 })?.price).toBeDefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: 12.345 })?.price).toBeDefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: 12.34 })).toBeUndefined();
  });

  it('đơn giá: đúng biên thì qua, âm hoặc vượt biên thì lỗi', () => {
    expect(errorsOf({ ...valid, cmtUnitPrice: 0 })).toBeUndefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: 0.01 })).toBeUndefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: 99_999_999.99 })).toBeUndefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: 100_000_000 })).toBeUndefined();
    expect(errorsOf({ ...valid, cmtUnitPrice: -0.01 })?.price).toBe('Đơn giá CMT không được âm.');
  });

  it.each([1, 1_000_000])('số lượng %d nằm trong biên thì qua', (qty) => {
    const rows = [{ key: 'a', color: 'Đỏ', cells: { S: { orderLineId: null, qty } } }];
    expect(errorsOf({ ...valid, rows })).toBeUndefined();
  });

  it.each([-1, 1.5])('số lượng %d thì báo lỗi đúng ô', (qty) => {
    const rows = [
      {
        key: 'a',
        color: 'Đỏ',
        cells: { S: { orderLineId: null, qty }, M: { orderLineId: null, qty: 5 } },
      },
    ];
    expect(errorsOf({ ...valid, rows })?.[qtyErrorKey('a', 'S')]).toBe(
      'Số lượng phải là số nguyên, không âm.',
    );
  });

  it('đơn đã vào sản xuất: số lượng cũ về 0 cũng là để trống', () => {
    const rows = [
      {
        key: 'a',
        color: 'Đỏ',
        cells: { S: { orderLineId: 1, qty: 0 }, M: { orderLineId: 2, qty: 1 } },
      },
    ];
    expect(errorsOf({ ...valid, rows }, true)?.[qtyErrorKey('a', 'S')]).toBeDefined();
  });

  it('màu: 50 ký tự thì qua, 51 ký tự thì lỗi', () => {
    const cells = { S: { orderLineId: null, qty: 1 } };
    expect(
      errorsOf({ ...valid, rows: [{ key: 'a', color: 'x'.repeat(50), cells }] }),
    ).toBeUndefined();
    expect(
      errorsOf({ ...valid, rows: [{ key: 'a', color: 'x'.repeat(51), cells }] })?.[
        colorErrorKey('a')
      ],
    ).toBe('Màu tối đa 50 ký tự.');
  });

  it('chọn mã hàng nhưng chưa có dòng nào thì báo lỗi', () => {
    expect(errorsOf({ ...valid, rows: emptyBlock.rows })?.lines).toBeDefined();
  });

  it('có số lượng mà thiếu màu, có màu mà thiếu số lượng thì báo đúng dòng', () => {
    const errors = errorsOf({
      ...valid,
      rows: [
        { key: 'a', color: ' ', cells: { S: { orderLineId: null, qty: 1 } } },
        { key: 'b', color: 'Xanh', cells: {} },
      ],
    });
    expect(errors?.[colorErrorKey('a')]).toBe('Vui lòng nhập màu.');
    expect(errors?.[colorErrorKey('b')]).toBe('Màu này chưa có số lượng.');
  });

  it('màu trùng nhau, khác hoa thường và khoảng trắng vẫn tính là trùng', () => {
    const cells = { S: { orderLineId: null, qty: 1 } };
    const errors = errorsOf({
      ...valid,
      rows: [
        { key: 'a', color: 'Đỏ', cells },
        { key: 'b', color: ' đỏ ', cells },
      ],
    });
    expect(errors?.[colorErrorKey('a')]).toBeUndefined();
    expect(errors?.[colorErrorKey('b')]).toBe('Màu này đã có trong mã hàng.');
  });

  it('số lượng vượt 1.000.000 thì báo lỗi đúng ô', () => {
    const errors = errorsOf({
      ...valid,
      rows: [{ key: 'a', color: 'Đỏ', cells: { M: { orderLineId: null, qty: 1_000_001 } } }],
    });
    expect(errors?.[qtyErrorKey('a', 'M')]).toBeDefined();
  });

  it('đơn đã vào sản xuất: dòng cũ không được để trống số lượng', () => {
    const block: ProductBlock = {
      ...valid,
      rows: [
        {
          key: 'a',
          color: 'Đỏ',
          cells: { S: { orderLineId: 1, qty: null }, M: { orderLineId: 2, qty: 5 } },
        },
      ],
    };
    expect(errorsOf(block, true)?.[qtyErrorKey('a', 'S')]).toBeDefined();
    expect(errorsOf(block, false)).toBeUndefined();
  });

  it('một mã hàng khai ở hai khối thì báo lỗi ở cả hai', () => {
    const errors = validateBlocks([valid, { ...valid, key: 'b2' }], false);
    expect(errors.b1?.product).toBeDefined();
    expect(errors.b2?.product).toBeDefined();
  });
});

describe('thông tin chung của đơn', () => {
  it('mã đơn chỉ gồm chữ, số và . _ -, tối đa 30 ký tự', () => {
    expect(orderCodeSchema.safeParse(' DH-2611.01_A ').success).toBe(true);
    expect(orderCodeSchema.safeParse('DH 01').success).toBe(false);
    expect(orderCodeSchema.safeParse('ĐH01').success).toBe(false);
    expect(orderCodeSchema.safeParse('   ').success).toBe(false);
    expect(orderCodeSchema.safeParse('A'.repeat(30)).success).toBe(true);
    expect(orderCodeSchema.safeParse(` ${'A'.repeat(30)} `).success).toBe(true);
    expect(orderCodeSchema.safeParse('A'.repeat(31)).success).toBe(false);
  });

  it('ghi chú tối đa 1000 ký tự', () => {
    expect(orderNoteSchema.safeParse('a'.repeat(1000)).success).toBe(true);
    expect(orderNoteSchema.safeParse('a'.repeat(1001)).success).toBe(false);
  });
});
