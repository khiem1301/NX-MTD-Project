import { AI_FILTER } from '../constants';
import type { GradeSuggestionItem } from '../types';
import { confirmCounts, filterSuggestions, periodDays } from './gradeSuggestion';

const item = (
  workerId: number,
  currentGradeCode: string | null,
  proposedGradeCode: string | null,
  extra: Partial<GradeSuggestionItem> = {},
): GradeSuggestionItem => ({
  workerId,
  workerCode: `CN${workerId}`,
  fullName: `Công nhân ${workerId}`,
  currentGradeCode,
  pendingGradeCode: null,
  metrics: {
    tenureMonths: 0,
    workDays: 0,
    workdayTotal: 0,
    workedHours: 0,
    overtimeHours: 0,
    outputQty: 0,
    operationCount: 0,
    avgSkillLevel: null,
    efficiencyPercent: null,
    reworkQty: 0,
    scrapQty: 0,
    defectRatePercent: null,
  },
  aiGradeCode: proposedGradeCode,
  aiReason: 'AI',
  proposedGradeCode,
  reason: 'AI',
  isEdited: false,
  direction: null,
  ...extra,
});

const items = [
  item(1, 'C', 'B', { pendingGradeCode: 'A', direction: 'Up' }),
  item(2, 'B', 'B', { direction: 'Same' }),
  item(3, null, 'D', { isEdited: true, direction: 'Up' }),
  item(4, 'A', 'C', { direction: 'Down' }),
];

describe('filterSuggestions', () => {
  it('filterSuggestions_ChangedFilter_ReturnsOnlyRowsWithNewGrade', () => {
    expect(filterSuggestions(items, { change: AI_FILTER.CHANGED }).map((i) => i.workerId)).toEqual([
      1, 3, 4,
    ]);
  });

  it('filterSuggestions_UpAndDownFilters_ReturnOnlyMatchingDirection', () => {
    expect(filterSuggestions(items, { change: AI_FILTER.UP }).map((i) => i.workerId)).toEqual([
      1, 3,
    ]);
    expect(filterSuggestions(items, { change: AI_FILTER.DOWN }).map((i) => i.workerId)).toEqual([
      4,
    ]);
  });

  it('filterSuggestions_KeywordAndGrade_MatchesNameAndProposedGrade', () => {
    expect(
      filterSuggestions(items, { keyword: 'nhân 2', grade: 'B' }).map((i) => i.workerId),
    ).toEqual([2]);
    expect(filterSuggestions(items, { change: AI_FILTER.PENDING }).map((i) => i.workerId)).toEqual([
      1,
    ]);
    expect(filterSuggestions(items, { change: AI_FILTER.EDITED }).map((i) => i.workerId)).toEqual([
      3,
    ]);
  });
});

describe('confirmCounts', () => {
  it('confirmCounts_MixedRows_CountsChangedAndReplacedPending', () => {
    expect(confirmCounts(items)).toEqual({ sent: 3, replaced: 1 });
  });
});

describe('periodDays', () => {
  it('periodDays_FromAndToDates_CountsBothEnds', () => {
    expect(periodDays('2026-08-23', '2026-11-20')).toBe(90);
    expect(periodDays('2026-11-20', '2026-11-20')).toBe(1);
  });
});
