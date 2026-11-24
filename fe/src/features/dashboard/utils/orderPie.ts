import type { OrderProgress } from '../types';

/** Màu từng chặng của biểu đồ tròn tiến độ (theo mockup trang chủ). */
export const PIE_COLOR = {
  cut: '#69b1ff',
  sew: '#95de64',
  qc: '#ffc069',
  ship: '#b37feb',
  rest: '#f0f0f0',
} as const;

export type PieStage = keyof typeof PIE_COLOR;

export type PieSlice = { stage: PieStage; qty: number; percent: number };

type Progress = Pick<
  OrderProgress,
  'orderedQty' | 'cutQty' | 'issuedToSewingQty' | 'finishedQty' | 'finishedReceivedQty'
>;

/**
 * Chia số lượng đặt thành các chặng loại trừ nhau theo vị trí hiện tại của hàng: đã cắt chưa cấp may (Cắt), đang may
 * (May), hoàn thiện chờ nhập kho (QC), đã nhập kho thành phẩm / giao (Giao), còn lại chưa cắt (rest). Tổng luôn bằng số đặt.
 */
export const orderPieSlices = (o: Progress): PieSlice[] => {
  const ordered = Math.max(0, o.orderedQty);
  const clamp = (v: number) => Math.min(ordered, Math.max(0, v));
  const cut = clamp(o.cutQty);
  const sewing = Math.min(cut, clamp(o.issuedToSewingQty));
  const finished = Math.min(sewing, clamp(o.finishedQty));
  const received = Math.min(finished, clamp(o.finishedReceivedQty));
  const qty: Record<PieStage, number> = {
    cut: cut - sewing,
    sew: sewing - finished,
    qc: finished - received,
    ship: received,
    rest: ordered - cut,
  };
  return (Object.keys(qty) as PieStage[]).map((stage) => ({
    stage,
    qty: qty[stage],
    percent: ordered > 0 ? Math.round((100 * qty[stage]) / ordered) : 0,
  }));
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
