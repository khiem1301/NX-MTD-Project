import { Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { PAGE_SIZE_OPTIONS } from '@/constants/pagination';
import { fill } from '@/utils/format';

const OPTIONS = PAGE_SIZE_OPTIONS.map((size) => ({
  value: size,
  label: fill(MESSAGES.common.perPage, size),
}));

type PageSizeSelectProps = { value: number; onChange: (size: number) => void };

/** Ô chọn số dòng mỗi trang, đặt ở góc trên bên phải của bảng. */
export function PageSizeSelect({ value, onChange }: PageSizeSelectProps) {
  return (
    <Select
      size="small"
      value={value}
      options={OPTIONS}
      onChange={onChange}
      aria-label={MESSAGES.common.pageSizeLabel}
      style={{ width: 110 }}
    />
  );
}
