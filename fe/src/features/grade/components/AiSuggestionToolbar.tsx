import { Input, Space, Tag, Typography } from 'antd';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import { fill, formatDate, formatDateTime } from '@/utils/format';
import { AI_FILTER_OPTIONS, AI_LABELS, type AiFilter } from '../constants';
import type { GradeSuggestion } from '../types';
import type { SuggestionFilters } from '../utils/gradeSuggestion';

type AiSuggestionToolbarProps = {
  draft: GradeSuggestion;
  filters: SuggestionFilters;
  gradeOptions: { value: string; label: string }[];
  onFiltersChange: (filters: SuggestionFilters) => void;
};

/** Thông tin bản gợi ý (kỳ số liệu, người tạo, hạn lưu) và bộ lọc dòng trong modal AI gợi ý. */
export function AiSuggestionToolbar({
  draft,
  filters,
  gradeOptions,
  onFiltersChange,
}: AiSuggestionToolbarProps) {
  return (
    <>
      <Space size={[16, 4]} wrap style={{ marginBottom: 12 }}>
        <Typography.Text type="secondary">
          {fill(AI_LABELS.period, formatDate(draft.periodFrom), formatDate(draft.periodTo))}
        </Typography.Text>
        <Typography.Text type="secondary">
          {fill(AI_LABELS.generatedAt, formatDateTime(draft.generatedAt), draft.generatedByName)}
        </Typography.Text>
        <Typography.Text type="secondary">
          {fill(AI_LABELS.expiresAt, formatDateTime(draft.expiresAt))}
        </Typography.Text>
        {draft.confirmedAt && (
          <Tag color="green">{fill(AI_LABELS.confirmedAt, formatDateTime(draft.confirmedAt))}</Tag>
        )}
      </Space>
      <TableToolbar
        filters={
          <>
            <Input.Search
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => onFiltersChange({ ...filters, keyword })}
              style={{ width: 240 }}
            />
            <FilterSelect<AiFilter>
              value={filters.change}
              placeholder={AI_LABELS.change}
              allLabel={AI_LABELS.filterAll}
              options={AI_FILTER_OPTIONS}
              onChange={(change) => onFiltersChange({ ...filters, change })}
              style={{ width: 200 }}
            />
            <FilterSelect<string>
              value={filters.grade}
              placeholder={AI_LABELS.filterGrade}
              prefixSelected
              options={gradeOptions}
              onChange={(grade) => onFiltersChange({ ...filters, grade })}
              style={{ width: 200 }}
            />
          </>
        }
      />
    </>
  );
}
