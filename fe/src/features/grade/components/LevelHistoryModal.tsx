import { useState } from 'react';
import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { Alert, Space, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import dayjs from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { GR_LABELS } from '../constants';
import { useDeleteLevel } from '../hooks/useGrade';
import type { GradeCode, GradeLevel } from '../types';
import { DataTable } from '@/components/common/DataTable';
import { DetailModal } from '@/components/common/DetailModal';
import { EditLevelModal } from './EditLevelModal';
import { levelColumns } from './levelColumns';

/** Lịch sử mức tiền của một bậc; mức chưa hiệu lực sửa / xóa được. */
export function LevelHistoryModal({
  code,
  rows,
  onClose,
}: {
  code: GradeCode | null;
  rows: GradeLevel[];
  onClose: () => void;
}) {
  const deleteLevel = useDeleteLevel();
  const [editing, setEditing] = useState<GradeLevel | null>(null);
  const list = rows.filter((l) => l.gradeCode === code?.gradeCode);
  const columns: TableColumnsType<GradeLevel> = [
    ...levelColumns(list),
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) =>
        dayjs(row.effectiveDate).isAfter(dayjs(), 'day') ? (
          <Space size={0} wrap>
            <IconAction
              title={MESSAGES.common.edit}
              icon={<EditOutlined />}
              onClick={() => setEditing(row)}
            />
            <ConfirmAction
              title={GR_LABELS.deleteLevelConfirm}
              onConfirm={() => deleteLevel.mutate(row.gradeLevelId)}
            >
              <IconAction title={MESSAGES.common.delete} danger icon={<DeleteOutlined />} />
            </ConfirmAction>
          </Space>
        ) : null,
    },
  ];

  return (
    <DetailModal
      open={code !== null}
      onClose={onClose}
      size="md"
      title={code ? `${GR_LABELS.levelHistory} - ${code.gradeCode} · ${code.gradeName}` : undefined}
    >
      <Alert type="info" showIcon style={{ marginBottom: 16 }} message={GR_LABELS.futureOnlyHint} />
      <DataTable rowKey="gradeLevelId" columns={columns} dataSource={list} pagination={false} />
      <EditLevelModal level={editing} onClose={() => setEditing(null)} />
    </DetailModal>
  );
}
