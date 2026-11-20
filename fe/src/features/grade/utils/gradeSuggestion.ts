import { AI_FILTER, type AiFilter } from '../constants';
import type { GradeSuggestionItem } from '../types';

/** Dòng sẽ thành đề xuất khi xác nhận: có bậc đề xuất và khác bậc hiện tại. */
export const isChanged = (item: GradeSuggestionItem): boolean =>
  item.proposedGradeCode !== null && item.proposedGradeCode !== item.currentGradeCode;

export type SuggestionFilters = { keyword?: string; change?: AiFilter; grade?: string };

const matchesChange = (item: GradeSuggestionItem, change?: AiFilter): boolean => {
  switch (change) {
    case AI_FILTER.CHANGED:
      return isChanged(item);
    case AI_FILTER.UNCHANGED:
      return !isChanged(item);
    case AI_FILTER.EDITED:
      return item.isEdited;
    case AI_FILTER.PENDING:
      return item.pendingGradeCode !== null;
    default:
      return true;
  }
};

/** Lọc trên trình duyệt: từ khóa theo mã / tên công nhân, loại thay đổi, bậc đề xuất. */
export const filterSuggestions = (
  items: GradeSuggestionItem[],
  { keyword, change, grade }: SuggestionFilters,
): GradeSuggestionItem[] => {
  const text = keyword?.trim().toLowerCase();
  return items.filter(
    (i) =>
      (!text ||
        i.workerCode.toLowerCase().includes(text) ||
        i.fullName.toLowerCase().includes(text)) &&
      matchesChange(i, change) &&
      (!grade || i.proposedGradeCode === grade),
  );
};

const MS_PER_DAY = 86_400_000;

/** Số ngày của kỳ số liệu (tính cả hai đầu), để ô chọn số ngày hiện đúng kỳ của bản gợi ý đang có. */
export const periodDays = (from: string, to: string): number =>
  Math.round((Date.parse(to) - Date.parse(from)) / MS_PER_DAY) + 1;

/** Số đề xuất sẽ gửi và số đề xuất đang chờ bị thay, để hiện trên nút / hộp xác nhận. */
export const confirmCounts = (items: GradeSuggestionItem[]) => {
  const changed = items.filter(isChanged);
  return { sent: changed.length, replaced: changed.filter((i) => i.pendingGradeCode).length };
};
