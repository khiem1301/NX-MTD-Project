import { Alert, Checkbox, Space, Tooltip } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';

const text = MESSAGES.common.importReplace;
const SHOWN = 8;

type ReplaceImportOptionProps = {
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  /** Dòng cũ sẽ bị bỏ theo bản xem trước (máy chủ tính). */
  removed?: readonly string[] | null;
  /** Dòng cũ không có trong file nhưng không bỏ được (đang được dùng): giữ nguyên. */
  blocked?: readonly string[] | null;
  /** Gọi tên dòng trong thông báo: "dòng", "NVL", "công đoạn". */
  unit?: string;
  /** true = dòng bị bỏ chuyển sang ngừng dùng (không xóa hẳn). */
  discontinue?: boolean;
};

const list = (items: readonly string[]) =>
  items.slice(0, SHOWN).join('; ') +
  (items.length > SHOWN ? ` ${fill(text.more, items.length - SHOWN)}` : '');

/**
 * Ô "Thay thế dữ liệu cũ" của hộp thoại nhập Excel (mặc định tắt = chỉ gộp). Bật thì liệt kê những dòng cũ sẽ bị bỏ để người dùng
 * xem trước khi nhập.
 */
export function ReplaceImportOption({
  checked,
  onChange,
  disabled,
  removed,
  blocked,
  unit = 'dòng',
  discontinue = false,
}: ReplaceImportOptionProps) {
  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Tooltip title={text.hint}>
        <Checkbox checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)}>
          {text.label}
        </Checkbox>
      </Tooltip>
      {checked && removed && removed.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={fill(discontinue ? text.willDiscontinue : text.willRemove, removed.length, unit)}
          description={list(removed)}
        />
      )}
      {checked && blocked && blocked.length > 0 && (
        <Alert
          type="info"
          showIcon
          message={fill(text.blocked, blocked.length, unit)}
          description={list(blocked)}
        />
      )}
    </Space>
  );
}
