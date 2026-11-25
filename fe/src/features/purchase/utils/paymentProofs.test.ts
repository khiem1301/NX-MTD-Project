import { newProofCount } from './paymentProofs';

const proof = (uploadedAt: string, purpose = 'PaymentProof') => ({ purpose, uploadedAt }) as never;
const pay = (createdAt: string) => ({ createdAt });

describe('newProofCount', () => {
  it('chưa thanh toán lần nào: mọi ảnh chứng từ đều tính', () => {
    expect(newProofCount([proof('2026-11-25T08:00:00')], [])).toBe(1);
    expect(newProofCount([], [])).toBe(0);
  });

  it('chỉ tính ảnh tải lên sau lần thanh toán gần nhất', () => {
    const payments = [pay('2026-11-25T09:00:00'), pay('2026-11-25T10:00:00')];
    const photos = [
      proof('2026-11-25T08:00:00'),
      proof('2026-11-25T09:30:00'),
      proof('2026-11-25T10:30:00'),
    ];
    expect(newProofCount(photos, payments)).toBe(1);
  });

  it('bỏ ảnh không phải chứng từ thanh toán', () => {
    expect(newProofCount([proof('2026-11-25T08:00:00', 'Photo')], [])).toBe(0);
  });
});
