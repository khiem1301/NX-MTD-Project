import { PIE_COLOR, orderPieSlices, pieGradient, sharePercents } from './orderPie';

const progress = {
  orderedQty: 100,
  cutQty: 80,
  issuedToSewingQty: 60,
  finishedQty: 40,
  finishedReceivedQty: 10,
  deliveredQty: 4,
};

describe('orderPieSlices', () => {
  it('chia số đặt thành các chặng loại trừ nhau, tổng bằng số đặt', () => {
    const slices = orderPieSlices(progress);
    const qty = Object.fromEntries(slices.map((s) => [s.stage, s.qty]));

    expect(qty).toEqual({ cut: 20, sew: 20, qc: 30, stock: 6, ship: 4, rest: 20 });
    expect(slices.reduce((sum, s) => sum + s.qty, 0)).toBe(100);
  });

  it('số liệu lệch (hoàn thiện nhiều hơn cấp may) không ra số âm hay vượt số đặt', () => {
    const slices = orderPieSlices({ ...progress, issuedToSewingQty: 10, finishedQty: 90 });

    expect(slices.every((s) => s.qty >= 0)).toBe(true);
    expect(slices.reduce((sum, s) => sum + s.qty, 0)).toBe(100);
  });

  it('nhập kho đủ nhưng chưa giao hết: Giao không phải 100%, phần còn lại là Kho', () => {
    const slices = orderPieSlices({
      orderedQty: 100,
      cutQty: 100,
      issuedToSewingQty: 100,
      finishedQty: 100,
      finishedReceivedQty: 100,
      deliveredQty: 40,
    });
    const percent = Object.fromEntries(slices.map((s) => [s.stage, s.percent]));

    expect(percent).toMatchObject({ stock: 60, ship: 40, rest: 0 });
  });

  it('đơn chưa có số lượng: mọi chặng bằng 0', () => {
    expect(orderPieSlices({ ...progress, orderedQty: 0 }).every((s) => s.qty === 0)).toBe(true);
  });
});

describe('pieGradient', () => {
  it('ghép các chặng có hàng thành conic-gradient theo tỉ lệ', () => {
    const css = pieGradient(
      orderPieSlices({ ...progress, orderedQty: 100, cutQty: 50, issuedToSewingQty: 0 }),
    );

    expect(css).toBe(`conic-gradient(${PIE_COLOR.cut} 0% 50%, ${PIE_COLOR.rest} 50% 100%)`);
  });

  it('không có số lượng thì xám trơn', () => {
    expect(pieGradient(orderPieSlices({ ...progress, orderedQty: 0 }))).toBe(PIE_COLOR.rest);
  });
});

describe('sharePercents', () => {
  it('cộng lại đúng 100%', () => {
    const p = sharePercents([1, 1, 1], 3);

    expect(p.reduce((a, b) => a + b, 0)).toBe(100);
  });

  it('chưa đủ toàn bộ thì không hiện 100% dù làm tròn lên', () => {
    expect(sharePercents([996, 4], 1000)).toEqual([99, 1]);
  });

  it('có hàng thì tối thiểu 1%, đủ toàn bộ thì 100%', () => {
    expect(sharePercents([1, 999], 1000)).toEqual([1, 99]);
    expect(sharePercents([50, 0], 50)).toEqual([100, 0]);
  });
});
