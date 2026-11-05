import {
  colorImageCountError,
  colorNameError,
  productCodeSchema,
  productDescriptionSchema,
  productNameSchema,
  toFieldPath,
} from './productValidation';

describe('ảnh theo màu', () => {
  const imgs = (n: number) => Array.from({ length: n }, (_, i) => `k${i}`);
  const row = (color: string) => ({ color, images: imgs(1) });

  it('mỗi màu có 1 đến 3 ảnh', () => {
    expect(colorImageCountError(imgs(1))).toBeNull();
    expect(colorImageCountError(imgs(3))).toBeNull();
    expect(colorImageCountError(undefined)).toBe('Chọn từ 1 đến 3 ảnh cho màu này.');
    expect(colorImageCountError([])).toBe('Chọn từ 1 đến 3 ảnh cho màu này.');
    expect(colorImageCountError(imgs(4))).toBe('Chọn từ 1 đến 3 ảnh cho màu này.');
  });

  it('màu bắt buộc và tối đa 50 ký tự', () => {
    expect(colorNameError([row('  ')], 0)).toBe('Vui lòng nhập màu.');
    expect(colorNameError([row('x'.repeat(50))], 0)).toBeNull();
    expect(colorNameError([row('x'.repeat(51))], 0)).toBe('Tên màu tối đa 50 ký tự.');
  });

  it('màu trùng dòng trên (không phân biệt hoa thường, khoảng trắng) báo ở dòng dưới', () => {
    const rows = [row('Đỏ'), row(' đỏ '), row('Xanh')];

    expect(colorNameError(rows, 0)).toBeNull();
    expect(colorNameError(rows, 1)).toBe('Màu "đỏ" đã có ở dòng trên.');
    expect(colorNameError(rows, 2)).toBeNull();
  });
});

describe('toFieldPath', () => {
  it('đổi tên lỗi BE thành đường dẫn field của form', () => {
    expect(toFieldPath('colorImages[0].Images')).toEqual(['colorImages', 0, 'images']);
    expect(toFieldPath('ProductCode')).toEqual(['productCode']);
  });
});

describe('mã hàng', () => {
  it('mã chỉ gồm chữ không dấu, số và . _ -, tối đa 30 ký tự tính sau khi trim', () => {
    expect(productCodeSchema.safeParse('AO-01.A_2').success).toBe(true);
    expect(productCodeSchema.safeParse(` ${'A'.repeat(30)} `).success).toBe(true);
    expect(productCodeSchema.safeParse('A'.repeat(31)).success).toBe(false);
    expect(productCodeSchema.safeParse('AO 01').success).toBe(false);
    expect(productCodeSchema.safeParse('ÁO01').success).toBe(false);
    expect(productCodeSchema.safeParse('AO/01').success).toBe(false);
    expect(productCodeSchema.safeParse('   ').success).toBe(false);
    expect(productCodeSchema.safeParse('').success).toBe(false);
  });

  it('tên bắt buộc và tối đa 200 ký tự', () => {
    expect(productNameSchema.safeParse('x'.repeat(200)).success).toBe(true);
    expect(productNameSchema.safeParse('x'.repeat(201)).success).toBe(false);
    expect(productNameSchema.safeParse('   ').success).toBe(false);
  });

  it('mô tả tối đa 1000 ký tự, để trống được', () => {
    expect(productDescriptionSchema.safeParse('').success).toBe(true);
    expect(productDescriptionSchema.safeParse('x'.repeat(1000)).success).toBe(true);
    expect(productDescriptionSchema.safeParse('x'.repeat(1001)).success).toBe(false);
  });
});
