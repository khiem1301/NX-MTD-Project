/** Màu (không phân biệt hoa thường, bỏ khoảng trắng đầu cuối) của đơn mà mã hàng chưa có định mức NVL; giữ cách viết đầu tiên. */
export const colorsWithoutNorm = (
  orderColors: readonly string[],
  normColors: readonly string[],
): string[] => {
  const key = (c: string) => c.trim().toUpperCase();
  const known = new Set(normColors.map(key));
  const seen = new Set<string>();
  const missing: string[] = [];
  for (const color of orderColors) {
    const k = key(color);
    if (!k || known.has(k) || seen.has(k)) continue;
    seen.add(k);
    missing.push(color.trim());
  }
  return missing;
};
