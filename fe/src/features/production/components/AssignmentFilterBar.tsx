import { Input, Segmented } from 'antd';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { fill } from '@/utils/format';
import { ASSIGNMENT_SEARCH_MAX_LENGTH, PRODUCTION_TEXT } from '../constants';
import {
  ASSIGNMENT_STATE,
  assignmentGroups,
  workerLoads,
  type AssignmentFilter,
  type AssignmentRow,
  type AssignmentState,
} from '../utils/assignmentBoard';

const text = PRODUCTION_TEXT.assignments;

type AssignmentFilterBarProps = {
  rows: AssignmentRow[];
  filter: AssignmentFilter;
  workerOptions: { value: number; label: string }[];
  onChange: (change: Partial<AssignmentFilter>) => void;
};

/** Bộ lọc của bảng phân công: tìm công đoạn, nhóm, công nhân (kèm số công đoạn đang làm) và tình trạng. */
export function AssignmentFilterBar({
  rows,
  filter,
  workerOptions,
  onChange,
}: AssignmentFilterBarProps) {
  const missing = rows.filter((r) => !r.isCovered).length;
  const loads = workerLoads(rows);
  const groups = assignmentGroups(rows);

  return (
    <TableToolbar
      filters={
        <>
          <Input.Search
            allowClear
            style={{ width: 220 }}
            placeholder={text.search}
            maxLength={ASSIGNMENT_SEARCH_MAX_LENGTH}
            value={filter.keyword ?? ''}
            onChange={(e) => onChange({ keyword: e.target.value })}
          />
          {groups.length > 0 && (
            <FilterSelect
              showSearch
              optionFilterProp="label"
              style={{ width: 200 }}
              popupMatchSelectWidth={false}
              placeholder={text.filterGroup}
              options={groups.map((g) => ({ value: g, label: g }))}
              value={filter.group}
              onChange={(group) => onChange({ group })}
            />
          )}
          <FilterSelect
            showSearch
            optionFilterProp="label"
            style={{ width: 220 }}
            popupMatchSelectWidth={false}
            placeholder={text.filterWorker}
            options={workerOptions.map((o) => ({
              value: o.value,
              label: fill(text.workerLoad, o.label, loads.get(o.value) ?? 0),
            }))}
            value={filter.workerId}
            onChange={(workerId) => onChange({ workerId })}
          />
          <Segmented<AssignmentState>
            value={filter.state}
            onChange={(state) => onChange({ state })}
            options={[
              { value: ASSIGNMENT_STATE.ALL, label: fill(text.stateAll, rows.length) },
              { value: ASSIGNMENT_STATE.MISSING, label: fill(text.stateMissing, missing) },
              {
                value: ASSIGNMENT_STATE.COVERED,
                label: fill(text.stateCovered, rows.length - missing),
              },
            ]}
          />
        </>
      }
    />
  );
}
