/** Bỏ dấu tiếng Việt, đ → d, chữ thường: "Đen" → "den" để gõ "d" / "đ" đều ra "Đen". */
export const foldVietnamese = (value: string): string =>
  value.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase().trim();

/** Màu khớp từ khóa (không dấu, theo đầu chuỗi hoặc đầu từ); màu khớp đầu chuỗi xếp trước. */
export const filterColors = (colors: readonly string[], keyword: string): string[] => {
  const k = foldVietnamese(keyword);
  if (!k) return [...colors];
  const starts: string[] = [];
  const words: string[] = [];
  for (const c of colors) {
    const f = foldVietnamese(c);
    if (f.startsWith(k)) starts.push(c);
    else if (f.split(/\s+/).some((w) => w.startsWith(k))) words.push(c);
  }
  return [...starts, ...words];
};
