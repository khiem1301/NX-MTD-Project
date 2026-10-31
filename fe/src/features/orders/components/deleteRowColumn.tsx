import type { Dispatch, SetStateAction } from 'react';
import { DeleteOutlined } from '@ant-design/icons';
import { Button } from 'antd';
import { MESSAGES } from '@/constants/messages';

/** Cột cuối của bảng nhập liệu: nút xóa dòng (cố định bên phải khi bảng cuộn ngang). */
export const deleteRowColumn = <T extends { key: string }>(set: Dispatch<SetStateAction<T[]>>) => ({
  title: '',
  width: 48,
  fixed: 'right' as const,
  render: (_: unknown, r: T) => (
    <Button
      type="text"
      danger
      icon={<DeleteOutlined />}
      aria-label={MESSAGES.common.delete}
      onClick={() => set((rows) => rows.filter((x) => x.key !== r.key))}
    />
  ),
});
