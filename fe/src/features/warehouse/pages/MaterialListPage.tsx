import { useState } from 'react';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { Button, Card, Popconfirm, Space, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { MaterialFormModal } from '../components/MaterialFormModal';
import {
  CATEGORY_LABEL,
  LABELS,
  MATERIAL_STATUS,
  MATERIAL_STATUS_LABEL,
  MATERIAL_TYPE,
  MATERIAL_TYPE_LABEL,
} from '../constants';
import { useChangeMaterialStatus, useMaterials } from '../hooks/useMaterials';
import type { Material } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; materialType?: string };

const TYPE_OPTIONS = Object.values(MATERIAL_TYPE).map((t) => ({
  value: t,
  label: MATERIAL_TYPE_LABEL[t],
}));

/** UC38: Thủ kho quản lý danh mục NVL (vải / phụ liệu). */
export function MaterialListPage() {
  const table = useTableQuery<Filters>({});
  const materials = useMaterials(table.params);
  const changeStatus = useChangeMaterialStatus();
  const [editing, setEditing] = useState<Material | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const openForm = (material: Material | null) => {
    setEditing(material);
    setFormOpen(true);
  };

  const columns: TableColumnsType<Material> = [
    { title: LABELS.materialCode, dataIndex: 'materialCode', width: 120 },
    { title: LABELS.materialName, dataIndex: 'materialName' },
    {
      title: LABELS.materialType,
      dataIndex: 'materialType',
      render: (v: string) => MATERIAL_TYPE_LABEL[v],
    },
    {
      title: LABELS.category,
      dataIndex: 'category',
      render: (v: string) => CATEGORY_LABEL[v] ?? v,
    },
    { title: LABELS.unit, dataIndex: 'unit', width: 100 },
    { title: LABELS.color, dataIndex: 'color', width: 120, render: (v: string) => v || '—' },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={MATERIAL_STATUS_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, m) => {
        const active = m.status === MATERIAL_STATUS.ACTIVE;
        const next = active ? MATERIAL_STATUS.DISCONTINUED : MATERIAL_STATUS.ACTIVE;
        return (
          <Space size={0} wrap>
            <Button type="link" icon={<EditOutlined />} onClick={() => openForm(m)}>
              {MESSAGES.common.edit}
            </Button>
            <Popconfirm
              title={active ? MESSAGES.warehouse.confirmDiscontinue : LABELS.reactivate}
              onConfirm={() => changeStatus.mutate({ id: m.materialId, status: next })}
            >
              <Button type="link" danger={active}>
                {active ? LABELS.discontinue : LABELS.reactivate}
              </Button>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  return (
    <Card
      title={LABELS.materialsTitle}
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {MESSAGES.common.create}
        </Button>
      }
    >
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 280 }}
            />
            <FilterSelect
              value={table.filters.materialType}
              placeholder={LABELS.materialType}
              options={TYPE_OPTIONS}
              onChange={(materialType?: string) => table.setFilters({ materialType })}
              style={{ width: 160 }}
            />
          </>
        }
      />
      <QueryError error={materials.error} onRetry={() => void materials.refetch()} />
      <DataTable
        rowKey="materialId"
        columns={columns}
        dataSource={materials.data?.items}
        loading={materials.isFetching}
        pagination={table.pagination(materials.data?.totalItems)}
      />
      <MaterialFormModal open={formOpen} material={editing} onClose={() => setFormOpen(false)} />
    </Card>
  );
}
