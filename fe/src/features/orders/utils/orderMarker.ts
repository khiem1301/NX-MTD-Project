import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { OrderMarkerProduct, OrderMarkerStatus, SaveOrderMarkersPayload } from '../types';
import { newKey } from './productSpec';

const text = MESSAGES.orders.markers;

/** Giới hạn khớp SaveOrderMarkersDtoValidator (OrderMarkerLimits) ở BE. */
export const ORDER_MARKER_LIMITS = {
  maxMarkers: 50,
  maxLengthM: 99999.99,
  maxPiecesPerPly: 999,
  maxCodeLength: 20,
} as const;
const CODE_PATTERN = /^[A-Za-z0-9._-]+$/;

/** Một bìa trên màn hình; `pieces` theo size của đơn, bỏ trống = size không có trên bìa. */
export type OrderMarkerRow = {
  key: string;
  productMarkerId: number | null;
  markerNo: number | undefined;
  markerCode: string;
  lengthM: number | null;
  status: OrderMarkerStatus;
  pieces: Record<string, number | null>;
};

const sizeKey = (size: string) => size.trim().toUpperCase();

export const toOrderMarkerRows = (product: OrderMarkerProduct): OrderMarkerRow[] =>
  product.markers.map((m) => ({
    key: newKey(),
    productMarkerId: m.productMarkerId,
    markerNo: m.markerNo,
    markerCode: m.markerCode,
    lengthM: m.lengthM,
    status: m.status,
    pieces: Object.fromEntries(m.ratios.map((r) => [sizeKey(r.sizeCode), r.piecesPerPly])),
  }));

/** Bìa mới: nhóm mặc định là nhóm duy nhất nếu mã hàng chỉ có một nhóm. */
export const newOrderMarkerRow = (product: OrderMarkerProduct): OrderMarkerRow => ({
  key: newKey(),
  productMarkerId: null,
  markerNo: product.markerGroups.length === 1 ? product.markerGroups[0] : undefined,
  markerCode: '',
  lengthM: null,
  status: 'Active',
  pieces: {},
});

const ratiosOf = (row: OrderMarkerRow, sizes: string[]) =>
  sizes.flatMap((sizeCode) => {
    const piecesPerPly = row.pieces[sizeKey(sizeCode)];
    return piecesPerPly ? [{ sizeCode, piecesPerPly }] : [];
  });

function rowProblem(row: OrderMarkerRow, no: number, sizes: string[]): string {
  const { maxCodeLength, maxLengthM, maxPiecesPerPly } = ORDER_MARKER_LIMITS;
  if (row.markerNo === undefined) return fill(text.groupRequired, no);
  const code = row.markerCode.trim();
  if (!code || code.length > maxCodeLength || !CODE_PATTERN.test(code))
    return fill(text.codeInvalid, no, maxCodeLength);
  if (row.lengthM === null || row.lengthM <= 0 || row.lengthM > maxLengthM)
    return fill(text.lengthInvalid, no);
  const pieces = Object.values(row.pieces).filter((p): p is number => p !== null);
  if (pieces.some((p) => p < 1 || p > maxPiecesPerPly))
    return fill(text.piecesInvalid, no, maxPiecesPerPly);
  return ratiosOf(row, sizes).length === 0 ? fill(text.ratioRequired, no) : '';
}

/** Lỗi nhập bìa của một mã hàng (hiện trước khi gửi); chuỗi rỗng = hợp lệ. `no` của bìa là thứ tự dòng. */
export function validateOrderMarkers(rows: OrderMarkerRow[], sizes: string[]): string {
  if (rows.length > ORDER_MARKER_LIMITS.maxMarkers)
    return fill(text.maxMarkers, ORDER_MARKER_LIMITS.maxMarkers);
  for (const [index, row] of rows.entries()) {
    const problem = rowProblem(row, index + 1, sizes);
    if (problem) return problem;
  }
  const codes = rows.map((r) => `${r.markerNo}|${r.markerCode.trim().toUpperCase()}`);
  return new Set(codes).size === codes.length ? '' : text.codeDuplicate;
}

export const toOrderMarkersPayload = (
  rows: OrderMarkerRow[],
  sizes: string[],
): SaveOrderMarkersPayload => ({
  markers: rows.map((r) => ({
    productMarkerId: r.productMarkerId,
    markerNo: r.markerNo ?? 0,
    markerCode: r.markerCode.trim().toUpperCase(),
    lengthM: r.lengthM ?? 0,
    status: r.status,
    ratios: ratiosOf(r, sizes),
  })),
});

export const piecesOf = (row: OrderMarkerRow, size: string) => row.pieces[sizeKey(size)] ?? null;
export const withPieces = (row: OrderMarkerRow, size: string, value: number | null) => ({
  pieces: { ...row.pieces, [sizeKey(size)]: value },
});

/**
 * Khóa dựng lại bảng bìa của một mã hàng: chỉ đổi khi nội dung bìa trên máy chủ đổi (vd sau khi lưu), không đổi mỗi lần tải lại
 * cùng dữ liệu — nếu không bảng bị dựng lại (giật hình) và mất phần đang nhập.
 */
export const markerVersion = (product: OrderMarkerProduct) =>
  `${product.productId}:${JSON.stringify(product.markers)}:${product.sizes.join(',')}:${product.markerGroups.join(',')}`;
