import { Select, Space } from 'antd';
import { MESSAGES } from '@/constants/messages';
import {
  REQUEST_STATUS_LABEL,
  REQUEST_TYPE_LABEL,
  type RequestStatus,
  type RequestType,
} from '@/constants/requestTypes';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

/** 'All' chỉ dùng ở hộp "Đơn cần xử lý": bỏ lọc trạng thái (BE mặc định chỉ hiện đơn đang mở). */
export type RequestFilterValue = {
  keyword?: string;
  requestType?: RequestType;
  status?: RequestStatus | 'All';
};

type RequestFiltersProps = {
  value: RequestFilterValue;
  onChange: (patch: RequestFilterValue) => void;
  /** Trang "Đơn cần xử lý": không chọn trạng thái = đang mở (BE mặc định); có thêm lựa chọn "Tất cả". */
  emptyStatusLabel?: string;
};

const TYPE_OPTIONS = (Object.keys(REQUEST_TYPE_LABEL) as RequestType[]).map((v) => ({
  value: v,
  label: REQUEST_TYPE_LABEL[v],
}));
const STATUS_OPTIONS = (Object.keys(REQUEST_STATUS_LABEL) as RequestStatus[]).map((v) => ({
  value: v,
  label: REQUEST_STATUS_LABEL[v],
}));

export function RequestFilters({ value, onChange, emptyStatusLabel }: RequestFiltersProps) {
  return (
    <Space wrap>
      <DebouncedSearch
        allowClear
        placeholder={MESSAGES.common.search}
        defaultValue={value.keyword}
        onSearch={(keyword) => onChange({ keyword: keyword.trim() || undefined })}
        style={{ width: 240 }}
      />
      <FilterSelect<RequestType>
        placeholder={MESSAGES.requests.type}
        value={value.requestType}
        options={TYPE_OPTIONS}
        onChange={(requestType) => onChange({ requestType })}
        style={{ width: 180 }}
      />
      <Select
        allowClear
        placeholder={emptyStatusLabel ?? MESSAGES.requests.status}
        value={value.status}
        options={
          emptyStatusLabel
            ? [{ value: 'All' as const, label: MESSAGES.requests.allStatuses }, ...STATUS_OPTIONS]
            : STATUS_OPTIONS
        }
        onChange={(status?: RequestStatus | 'All') => onChange({ status })}
        style={{ width: 180 }}
      />
    </Space>
  );
}
