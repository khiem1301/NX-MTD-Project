import { DeleteOutlined } from '@ant-design/icons';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';

/** Cột cuối của bảng nhập liệu: nút xóa dòng (cố định bên phải khi bảng cuộn ngang). */
export const deleteRowColumn = <T extends { key: string }>(onDelete: (key: string) => void) => ({
  title: '',
  width: 48,
  fixed: 'right' as const,
  render: (_: unknown, r: T) => (
    <IconAction
      title={MESSAGES.common.delete}
      danger
      icon={<DeleteOutlined />}
      onClick={() => onDelete(r.key)}
    />
  ),
});
