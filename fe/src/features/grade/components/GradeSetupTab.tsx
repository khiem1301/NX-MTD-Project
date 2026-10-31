import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { normalizeText } from '@/utils/text';
import { GR_LABELS } from '../constants';
import {
  useDeleteGradeCode,
  useGradeCodes,
  useGradeLevels,
  useUpdateGradeCode,
} from '../hooks/useGrade';
import type { GradeCode } from '../types';
import { ChangeAmountModal } from './ChangeAmountModal';
import { gradeCodeColumns } from './gradeCodeColumns';
import { GradeCodeFormModal } from './GradeCodeFormModal';
import { LevelHistoryModal } from './LevelHistoryModal';

/** Admin: bậc thợ và mức tiền bậc trong một bảng (mỗi dòng một bậc). Thêm bậc kèm luôn mức tiền đầu tiên. */
export function GradeSetupTab() {
  const codes = useGradeCodes();
  const levels = useGradeLevels();
  const update = useUpdateGradeCode();
  const remove = useDeleteGradeCode();
  const [editing, setEditing] = useState<GradeCode | 'new' | null>(null);
  const [changing, setChanging] = useState<GradeCode | null>(null);
  const [viewing, setViewing] = useState<GradeCode | null>(null);
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState<GradeCode['status']>();
  const all = levels.data ?? [];
  const needle = normalizeText(keyword.trim());
  const visible = (codes.data ?? []).filter(
    (c) =>
      (statusFilter === undefined || c.status === statusFilter) &&
      (needle === '' || normalizeText(`${c.gradeCode} ${c.gradeName}`).includes(needle)),
  );

  const columns = gradeCodeColumns({
    levels: all,
    onView: setViewing,
    onChangeAmount: setChanging,
    onEdit: setEditing,
    onToggleStatus: (row) =>
      update.mutate({
        code: row.gradeCode,
        body: { gradeName: row.gradeName, status: row.status === 'Active' ? 'Inactive' : 'Active' },
      }),
    onRemove: (row) => remove.mutate(row.gradeCode),
  });

  return (
    <>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={GR_LABELS.searchGrade}
              onSearch={(k) => setKeyword(k)}
              style={{ width: 260 }}
            />
            <FilterSelect
              value={statusFilter}
              placeholder={GR_LABELS.status}
              options={[
                { value: 'Active', label: GR_LABELS.active },
                { value: 'Inactive', label: GR_LABELS.inactive },
              ]}
              onChange={(v?: GradeCode['status']) => setStatusFilter(v)}
              style={{ width: 160 }}
            />
          </>
        }
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>
            {GR_LABELS.addCode}
          </Button>
        }
      />
      <QueryError error={codes.error ?? levels.error} onRetry={() => void codes.refetch()} />
      <DataTable
        rowKey="gradeCode"
        columns={columns}
        dataSource={visible}
        loading={codes.isFetching || levels.isFetching}
        pagination={false}
      />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>
        {GR_LABELS.codeHint}
      </Typography.Paragraph>
      <GradeCodeFormModal editing={editing} onClose={() => setEditing(null)} />
      <ChangeAmountModal code={changing} rows={all} onClose={() => setChanging(null)} />
      <LevelHistoryModal code={viewing} rows={all} onClose={() => setViewing(null)} />
    </>
  );
}
