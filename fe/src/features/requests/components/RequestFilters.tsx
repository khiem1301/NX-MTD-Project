import { Input, Select, Space } from 'antd';
import { MESSAGES } from '@/constants/messages';
import {
  REQUEST_STATUS_LABEL,
  REQUEST_TYPE_LABEL,
  type RequestStatus,
  type RequestType,
} from '@/constants/requestTypes';

export type RequestFilterValue = { keyword?: string; requestType?: RequestType; status?: RequestStatus };

type RequestFiltersProps = {
  value: RequestFilterValue;
  onChange: (patch: RequestFilterValue) => void;
  /** Trang "Đơn cần xử lý": không chọn trạng thái = đang mở (BE mặc định). */
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
      <Input.Search
        allowClear
        placeholder={MESSAGES.common.search}
        defaultValue={value.keyword}
        onSearch={(keyword) => onChange({ keyword: keyword.trim() || undefined })}
        style={{ width: 240 }}
      />
      <Select
        allowClear
        placeholder={MESSAGES.requests.type}
        value={value.requestType}
        options={TYPE_OPTIONS}
        onChange={(requestType?: RequestType) => onChange({ requestType })}
        style={{ width: 180 }}
      />
      <Select
        allowClear
        placeholder={emptyStatusLabel ?? MESSAGES.requests.status}
        value={value.status}
        options={STATUS_OPTIONS}
        onChange={(status?: RequestStatus) => onChange({ status })}
        style={{ width: 180 }}
      />
    </Space>
  );
}
