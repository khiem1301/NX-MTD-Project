import {
  productCodeSchema,
  productDescriptionSchema,
  productNameSchema,
  toFieldPath,
} from './productValidation';

describe('toFieldPath', () => {
  it('đổi tên lỗi BE thành đường dẫn field của form', () => {
    expect(toFieldPath('ExtraImages[0]')).toEqual(['extraImages', 0]);
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
