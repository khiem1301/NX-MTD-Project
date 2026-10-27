import { Select, type SelectProps } from 'antd';
import { useMemo } from 'react';

/** Giá trị nội bộ của lựa chọn "Tất cả" (không bao giờ gửi lên API). */
const ALL = '__all__';

type Value = string | number;

type FilterSelectProps<V extends Value> = Omit<SelectProps<V | typeof ALL>, 'value' | 'onChange' | 'allowClear' | 'options' | 'defaultValue'> & {
  options: { value: V; label: React.ReactNode }[];
  /** Giá trị đang lọc; `undefined` = Tất cả. */
  value?: V;
  /** Nhận `undefined` khi chọn "Tất cả". */
  onChange: (value: V | undefined) => void;
  /** Nhãn của lựa chọn tất cả. */
  allLabel?: string;
};

/**
 * Select lọc danh sách: luôn có lựa chọn "Tất cả", chọn sẵn khi chưa lọc, không cần nút xóa.
 * Chọn "Tất cả" trả `undefined` để bỏ điều kiện lọc.
 */
export function FilterSelect<V extends Value>({
  options,
  value,
  onChange,
  allLabel = 'Tất cả',
  ...rest
}: FilterSelectProps<V>) {
  const withAll = useMemo(() => [{ value: ALL as V | typeof ALL, label: allLabel }, ...options], [options, allLabel]);
  return (
    <Select<V | typeof ALL>
      {...rest}
      options={withAll}
      // Chưa lọc thì ô hiện "<tên bộ lọc>: Tất cả" để người dùng biết đang lọc theo gì.
      labelRender={(o) =>
        o.value === ALL && typeof rest.placeholder === 'string' ? `${rest.placeholder}: ${allLabel}` : o.label
      }
      value={value ?? ALL}
      onChange={(v) => onChange(v === ALL ? undefined : (v as V))}
    />
  );
}
