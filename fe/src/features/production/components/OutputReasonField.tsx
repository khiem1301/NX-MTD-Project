import { Input, Space, Typography } from 'antd';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.output;
const REASON_MAX_LENGTH = 500;

type OutputReasonFieldProps = {
  value: string;
  onChange: (value: string) => void;
  /** Đã có số sửa mà chưa ghi lý do: tô đỏ ô và báo. */
  showError: boolean;
};

/** Ô lý do sửa sản lượng của Quản đốc trong khung nhập của công nhân (bắt buộc, lưu vào nhật ký). */
export function OutputReasonField({ value, onChange, showError }: OutputReasonFieldProps) {
  return (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      <Input.TextArea
        rows={2}
        maxLength={REASON_MAX_LENGTH}
        showCount
        aria-label={text.reason}
        placeholder={text.reason}
        status={showError ? 'error' : undefined}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {showError && <Typography.Text type="danger">{text.reasonRequired}</Typography.Text>}
    </Space>
  );
}
