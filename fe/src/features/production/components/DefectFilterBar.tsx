import { message } from 'antd';
import type { Dayjs } from 'dayjs';
import { AppRangePicker } from '@/components/common/DateInputs';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { toApiDate } from '@/utils/format';
import { DEFECT_MAX_RANGE_DAYS, PRODUCTION_TEXT } from '../constants';
import type { DefectFilters } from '../types';

const text = PRODUCTION_TEXT.defects;

const TYPE_OPTIONS = (Object.keys(text.type) as (keyof typeof text.type)[]).map((value) => ({
  value,
  label: text.type[value].text,
}));
const STATUS_OPTIONS = [
  { value: 'Recorded' as const, label: text.status.Recorded.text },
  { value: 'Confirmed' as const, label: text.status.Confirmed.text },
  { value: 'Cancelled' as const, label: text.status.Cancelled.text },
];

/** Bộ lọc Tổ: chỉ Quản đốc thấy, nên danh sách tổ chỉ được tải khi hiện ô này. */
function TeamFilter({ value, onChange }: { value?: number; onChange: (id?: number) => void }) {
  const teams = useTeamLookup();
  return (
    <FilterSelect<number>
      showSearch
      optionFilterProp="label"
      style={{ width: 170 }}
      placeholder={text.filters.team}
      loading={teams.isLoading}
      options={(teams.data ?? [])
        .filter((t) => t.teamType === 'Sewing')
        .map((t) => ({ value: t.teamId, label: t.teamName }))}
      value={value}
      onChange={onChange}
    />
  );
}

type DefectFilterBarProps = {
  filters: DefectFilters;
  isManager: boolean;
  onChange: (patch: Partial<DefectFilters>) => void;
};

/** Thanh lọc phiếu hàng hỏng, tách khỏi bảng: tìm kiếm, khoảng ngày, loại, tình trạng phiếu, phản đối (và tổ cho Quản đốc). */
export function DefectFilterBar({ filters, isManager, onChange }: DefectFilterBarProps) {
  const changeRange = (range: [Dayjs | null, Dayjs | null] | null) => {
    const [from, to] = range ?? [null, null];
    if (from && to && to.diff(from, 'day') > DEFECT_MAX_RANGE_DAYS) {
      void message.warning(text.rangeTooLong);
      return;
    }
    onChange({ from: from ? toApiDate(from) : undefined, to: to ? toApiDate(to) : undefined });
  };

  return (
    <TableToolbar
      filters={
        <>
          <DebouncedSearch
            allowClear
            style={{ width: 280 }}
            placeholder={text.searchPlaceholder}
            onSearch={(keyword) => onChange({ keyword: keyword.trim() || undefined })}
          />
          <AppRangePicker
            allowClear
            placeholder={text.filters.range}
            onChange={(v) => changeRange(v as [Dayjs | null, Dayjs | null] | null)}
          />
          {isManager && (
            <TeamFilter
              value={filters.sewingTeamId}
              onChange={(sewingTeamId) => onChange({ sewingTeamId })}
            />
          )}
          <FilterSelect
            style={{ width: 150 }}
            placeholder={text.filters.type}
            options={TYPE_OPTIONS}
            value={filters.defectType}
            onChange={(defectType) => onChange({ defectType })}
          />
          <FilterSelect
            style={{ width: 130 }}
            placeholder={text.filters.status}
            options={STATUS_OPTIONS}
            value={filters.status}
            onChange={(status) => onChange({ status })}
          />
          <FilterSelect
            style={{ width: 240 }}
            prefixSelected
            placeholder={text.filters.objection}
            options={text.objectionOptions}
            value={filters.objection}
            onChange={(objection) => onChange({ objection })}
          />
        </>
      }
    />
  );
}
