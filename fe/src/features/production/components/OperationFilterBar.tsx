import { Input, Space } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { FilterSelect } from '@/components/common/FilterSelect';
import { fill } from '@/utils/format';
import { OPERATION_LIMITS, PRODUCTION_TEXT } from '../constants';
import type { OperationGroup, OperationStatus } from '../types';
import {
  NO_GROUP,
  RATE_STATE,
  type OperationCounts,
  type OperationFilter,
  type RateState,
} from '../utils/operationBoard';
import { ProductSelect } from './ProductSelect';

const text = PRODUCTION_TEXT.operations;

const STATUS_OPTIONS = Object.entries(text.status).map(([value, label]) => ({
  value: value as OperationStatus,
  label: label.text,
}));

type OperationFilterBarProps = {
  productId: number | null;
  onProductChange: (id: number | undefined) => void;
  filter: OperationFilter;
  onFilterChange: (change: OperationFilter) => void;
  groups: OperationGroup[];
  counts: OperationCounts;
};

/** Chọn mã hàng và lọc công đoạn theo nhóm, từ khóa, trạng thái. */
export function OperationFilterBar({
  productId,
  onProductChange,
  filter,
  onFilterChange,
  groups,
  counts,
}: OperationFilterBarProps) {
  return (
    <Space wrap>
      <ProductSelect
        value={productId ?? undefined}
        onChange={onProductChange}
        placeholder={PRODUCTION_TEXT.selectProduct}
      />
      <FilterSelect<number>
        placeholder={text.filterGroup}
        value={filter.groupId}
        onChange={(groupId) => onFilterChange({ groupId })}
        options={[
          ...groups.map((g) => ({ value: g.operationGroupId, label: g.groupName })),
          { value: NO_GROUP, label: fill(text.noGroupFilter, counts.ungrouped) },
        ]}
        style={{ width: 170 }}
      />
      <FilterSelect<RateState>
        placeholder={text.rateFilter.label}
        value={filter.rateState}
        onChange={(rateState) => onFilterChange({ rateState })}
        options={[
          { value: RATE_STATE.MISSING, label: fill(text.rateFilter.missing, counts.missing) },
          { value: RATE_STATE.DRAFT, label: fill(text.rateFilter.draft, counts.draft) },
          { value: RATE_STATE.PENDING, label: fill(text.rateFilter.pending, counts.pending) },
          { value: RATE_STATE.REJECTED, label: fill(text.rateFilter.rejected, counts.rejected) },
        ]}
        style={{ width: 190 }}
      />
      <FilterSelect<OperationStatus>
        placeholder={text.columns.status}
        value={filter.status}
        onChange={(status) => onFilterChange({ status })}
        options={STATUS_OPTIONS}
        style={{ width: 170 }}
      />
      <Input
        allowClear
        prefix={<SearchOutlined />}
        placeholder={text.searchOperation}
        maxLength={OPERATION_LIMITS.maxSearch}
        value={filter.keyword ?? ''}
        onChange={(e) => onFilterChange({ keyword: e.target.value })}
        style={{ width: 210 }}
      />
    </Space>
  );
}
