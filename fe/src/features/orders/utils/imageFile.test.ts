import { isAcceptableImage, MAX_IMAGE_BYTES } from './imageFile';

describe('isAcceptableImage', () => {
  it('nhận ảnh đúng 5 MB, không nhận ảnh lớn hơn dù chỉ 1 byte', () => {
    expect(isAcceptableImage({ type: 'image/png', size: MAX_IMAGE_BYTES })).toBe(true);
    expect(isAcceptableImage({ type: 'image/png', size: MAX_IMAGE_BYTES + 1 })).toBe(false);
  });

  it('không nhận tệp không phải ảnh', () => {
    expect(isAcceptableImage({ type: 'application/pdf', size: 10 })).toBe(false);
    expect(isAcceptableImage({ type: '', size: 10 })).toBe(false);
  });
});
