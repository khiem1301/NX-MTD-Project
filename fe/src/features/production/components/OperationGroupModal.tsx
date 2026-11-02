import { useState } from 'react';
import { CheckOutlined, CloseOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { Button, Input, Modal, Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { OPERATION_LIMITS, PRODUCTION_TEXT } from '../constants';
import {
  useCreateOperationGroup,
  useDeleteOperationGroup,
  useOperationGroups,
  useRenameOperationGroup,
} from '../hooks/useProduction';
import type { OperationGroup } from '../types';

const text = PRODUCTION_TEXT.operations.groups;

type OperationGroupModalProps = { productId: number; productLabel: string; onClose: () => void };

/** DEV-ALL-50: thêm, đổi tên, xóa nhóm công đoạn của mã hàng. Trang chỉ gắn hộp thoại khi đang mở nên state tự reset. */
export function OperationGroupModal({
  productId,
  productLabel,
  onClose,
}: OperationGroupModalProps) {
  const groups = useOperationGroups(productId);
  const create = useCreateOperationGroup();
  const rename = useRenameOperationGroup();
  const remove = useDeleteOperationGroup();
  const [newName, setNewName] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] = useState('');

  const add = () =>
    create.mutate({ productId, groupName: newName.trim() }, { onSuccess: () => setNewName('') });
  const saveRename = () => {
    if (editingId === null || draft.trim() === '') return;
    rename.mutate(
      { id: editingId, groupName: draft.trim() },
      { onSuccess: () => setEditingId(null) },
    );
  };

  const columns: ColumnsType<OperationGroup> = [
    {
      title: text.name,
      key: 'name',
      render: (_, g) =>
        editingId === g.operationGroupId ? (
          <Input
            autoFocus
            aria-label={text.rename}
            maxLength={OPERATION_LIMITS.maxGroupName}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onPressEnter={saveRename}
          />
        ) : (
          g.groupName
        ),
    },
    { title: text.count, dataIndex: 'operationCount', align: 'right', width: 120 },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      width: 110,
      render: (_, g) =>
        editingId === g.operationGroupId ? (
          <Space size={0}>
            <IconAction
              title={MESSAGES.common.save}
              icon={<CheckOutlined />}
              disabled={draft.trim() === '' || rename.isPending}
              onClick={saveRename}
            />
            <IconAction
              title={MESSAGES.common.cancel}
              icon={<CloseOutlined />}
              onClick={() => setEditingId(null)}
            />
          </Space>
        ) : (
          <Space size={0}>
            <IconAction
              title={text.rename}
              icon={<EditOutlined />}
              onClick={() => {
                setEditingId(g.operationGroupId);
                setDraft(g.groupName);
              }}
            />
            <ConfirmAction
              title={fill(text.removeConfirm, g.operationCount)}
              onConfirm={() => remove.mutate(g.operationGroupId)}
            >
              <IconAction title={text.remove} icon={<DeleteOutlined />} danger />
            </ConfirmAction>
          </Space>
        ),
    },
  ];

  return (
    <Modal
      open
      width={MODAL_WIDTH.md}
      title={fill(text.title, productLabel)}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Typography.Text type="secondary">{text.hint}</Typography.Text>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            aria-label={text.addPlaceholder}
            placeholder={text.addPlaceholder}
            maxLength={OPERATION_LIMITS.maxGroupName}
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onPressEnter={() => {
              if (newName.trim() !== '') add();
            }}
          />
          <Button
            type="primary"
            disabled={newName.trim() === ''}
            loading={create.isPending}
            onClick={add}
          >
            {text.add}
          </Button>
        </Space.Compact>
        <QueryError error={groups.error} onRetry={() => void groups.refetch()} />
        <DataTable<OperationGroup>
          rowKey="operationGroupId"
          size="small"
          columns={columns}
          dataSource={groups.data ?? []}
          loading={groups.isLoading}
          locale={{ emptyText: text.empty }}
          pagination={false}
        />
      </Space>
    </Modal>
  );
}
