import { formatQty } from '@/utils/format';

/** Khổ vải nhập bằng mét, API và cột Material.WidthCm lưu cm. */
const CM_PER_METER = 100;

/** Khổ vải nhập tối đa (m): cột WidthCm là DECIMAL(7,2). */
export const MAX_FABRIC_WIDTH_M = 999.99;

/** Làm tròn 2 số lẻ để bỏ sai số của phép tính số thực (vd 1,6 × 100). */
const round2 = (value: number) => Math.round(value * 100) / 100;

/** Khổ vải để hiển thị, vd "1,5 m"; chưa có thì "—". */
export const formatFabricWidth = (cm: number | null | undefined): string =>
  cm ? `${formatQty(cmToMeters(cm))} m` : '—';

export const cmToMeters = (cm: number | null | undefined): number | null =>
  cm === null || cm === undefined ? null : round2(cm / CM_PER_METER);

export const metersToCm = (meters: number | null | undefined): number | null =>
  meters === null || meters === undefined ? null : round2(meters * CM_PER_METER);
