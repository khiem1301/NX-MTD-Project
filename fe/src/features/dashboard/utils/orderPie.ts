import type { OrderProgress } from '../types';

/** Màu từng chặng của biểu đồ tròn tiến độ (theo mockup trang chủ). */
export const PIE_COLOR = {
  cut: '#69b1ff',
  sew: '#95de64',
  qc: '#ffc069',
  stock: '#5cdbd3',
  ship: '#b37feb',
  rest: '#f0f0f0',
} as const;

export type PieStage = keyof typeof PIE_COLOR;

export type PieSlice = { stage: PieStage; qty: number; percent: number };

type Progress = Pick<
  OrderProgress,
  | 'orderedQty'
  | 'cutQty'
  | 'issuedToSewingQty'
  | 'finishedQty'
  | 'finishedReceivedQty'
  | 'deliveredQty'
>;

/** Tối đa của một phần chưa đủ toàn bộ: không hiện 100% khi chưa xong hết. */
const MAX_PARTIAL_PERCENT = 99;

/**
 * Phần trăm nguyên của các phần so với tổng, cộng lại đúng 100: lấy phần nguyên, phần còn thiếu chia cho các phần có số lẻ lớn nhất.
 * Phần có hàng thì tối thiểu 1%, phần chưa đủ toàn bộ thì tối đa 99% (không làm tròn lên 100% khi chưa xong).
 */
export const sharePercents = (qtys: number[], total: number): number[] => {
  if (total <= 0) return qtys.map(() => 0);
  const exact = qtys.map((q) => (100 * q) / total);
  const out = exact.map((e, i) => {
    const q = qtys[i] ?? 0;
    if (q <= 0) return 0;
    if (q >= total) return 100;
    return Math.min(MAX_PARTIAL_PERCENT, Math.max(1, Math.floor(e)));
  });
  let left = 100 - out.reduce((a, b) => a + b, 0);
  const order = exact
    .map((e, i) => ({ i, frac: e - Math.floor(e) }))
    .filter(({ i }) => (qtys[i] ?? 0) > 0 && (qtys[i] ?? 0) < total)
    .sort((a, b) => b.frac - a.frac);
  for (const { i } of order) {
    if (left <= 0) break;
    if ((out[i] ?? 0) < MAX_PARTIAL_PERCENT) {
      out[i] = (out[i] ?? 0) + 1;
      left--;
    }
  }
  return out;
};

/**
 * Chia số lượng đặt thành các chặng loại trừ nhau theo vị trí hiện tại của hàng: đã cắt chưa cấp may (Cắt), đang may
 * (May), hoàn thiện chờ nhập kho (QC), đã nhập kho chờ giao (Kho), đã giao khách (Giao), còn lại chưa cắt (rest). Tổng luôn bằng số đặt.
 */
export const orderPieSlices = (o: Progress): PieSlice[] => {
  const ordered = Math.max(0, o.orderedQty);
  const clamp = (v: number) => Math.min(ordered, Math.max(0, v));
  const cut = clamp(o.cutQty);
  const sewing = Math.min(cut, clamp(o.issuedToSewingQty));
  const finished = Math.min(sewing, clamp(o.finishedQty));
  const received = Math.min(finished, clamp(o.finishedReceivedQty));
  const delivered = Math.min(received, clamp(o.deliveredQty));
  const qty: Record<PieStage, number> = {
    cut: cut - sewing,
    sew: sewing - finished,
    qc: finished - received,
    stock: received - delivered,
    ship: delivered,
    rest: ordered - cut,
  };
  const stages = Object.keys(qty) as PieStage[];
  const percents = sharePercents(
    stages.map((stage) => qty[stage]),
    ordered,
  );
  return stages.map((stage, i) => ({ stage, qty: qty[stage], percent: percents[i] ?? 0 }));
};

/** Giá trị `background` CSS (conic-gradient) cho các chặng; đơn chưa có số lượng thì xám trơn. */
export const pieGradient = (slices: PieSlice[]): string => {
  const total = slices.reduce((s, x) => s + x.qty, 0);
  if (total <= 0) return PIE_COLOR.rest;
  let from = 0;
  const parts = slices
    .filter((s) => s.qty > 0)
    .map((s) => {
      const to = from + (100 * s.qty) / total;
      const part = `${PIE_COLOR[s.stage]} ${from}% ${to}%`;
      from = to;
      return part;
    });
  return `conic-gradient(${parts.join(', ')})`;
};
