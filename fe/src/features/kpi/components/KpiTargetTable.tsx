import { useState } from 'react';
import { Button, Progress, Space, Tag, Tooltip } from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { IconAction } from '@/components/common/IconAction';
import type { ColumnsType } from 'antd/es/table';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  EditOutlined,
  PlusOutlined,
} from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { fill, formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useCancelKpiTarget, useKpiTargets } from '../hooks/useKpi';
import { FinishOrderModal } from './FinishOrderModal';
import { KpiCalendarModal } from './KpiCalendarModal';
import type { KpiTarget, KpiTargetFilter, KpiTargetStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';

type Props = {
  filter: KpiTargetFilter;
  onFilterChange: (filter: KpiTargetFilter) => void;
  onAdd?: () => void;
  onEdit?: (target: KpiTarget) => void;
};

const STATUS_OPTIONS = (
  Object.entries(KPI_TEXT.statusLabel) as [KpiTargetStatus, { text: string }][]
).map(([value, { text }]) => ({ value, label: text }));

/** Chỉ tiêu đạt: xanh; trên 70%: đang tiến triển; còn lại: cảnh báo. Vượt 100% vẫn vẽ đầy thanh. */
const progressStatus = (percent: number): 'success' | 'normal' | 'exception' =>
  percent >= 100 ? 'success' : percent >= 70 ? 'normal' : 'exception';

/** UC19 / UC21 — danh sách chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu, lọc theo trạng thái + từ khóa. */
export function KpiTargetTable({ filter, onFilterChange, onAdd, onEdit }: Props) {
  const targets = useKpiTargets(filter);
  const cancel = useCancelKpiTarget();
  const [finishing, setFinishing] = useState<KpiTarget | null>(null);
  const [calendarOf, setCalendarOf] = useState<KpiTarget | null>(null);

  const columns: ColumnsType<KpiTarget> = [
    { title: KPI_TEXT.table.order, dataIndex: 'orderCode' },
    {
      title: KPI_TEXT.table.product,
      dataIndex: 'productCode',
      render: (code: string, row) => <Tooltip title={row.productName}>{code}</Tooltip>,
    },
    { title: KPI_TEXT.table.sewingTeam, dataIndex: 'sewingTeamName' },
    {
      title: KPI_TEXT.table.period,
      key: 'period',
      render: (_, row) => `${formatDate(row.startDate)} - ${formatDate(row.endDate)}`,
    },
    {
      title: KPI_TEXT.table.progress,
      key: 'progress',
      width: 240,
      render: (_, row) => (
        <Progress
          percent={Math.min(100, Math.round(row.percentAchieved))}
          format={() =>
            fill(
              KPI_TEXT.table.progressValue,
              formatQty(row.actualFinishedQty),
              formatQty(row.targetFinishedQty),
              formatQty(row.percentAchieved),
            )
          }
          status={progressStatus(row.percentAchieved)}
          size="small"
        />
      ),
    },
    {
      title: KPI_TEXT.table.status,
      key: 'status',
      render: (_, row) => {
        // Đã có kết quả thì chỉ tiêu đã đóng, chỉ cần hiện kết quả.
        const label = row.result
          ? KPI_TEXT.resultLabel[row.result]
          : KPI_TEXT.statusLabel[row.status];
        return <Tag color={label.color}>{label.text}</Tag>;
      },
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0}>
          <IconAction
            title={KPI_TEXT.calendar.button}
            icon={<CalendarOutlined />}
            onClick={() => setCalendarOf(row)}
          />
          {row.status === 'Active' && (
            <>
              <IconAction
                title={KPI_TEXT.edit}
                icon={<EditOutlined />}
                onClick={() => onEdit?.(row)}
              />
              <IconAction
                title={KPI_TEXT.finish.button}
                icon={<CheckCircleOutlined />}
                onClick={() => setFinishing(row)}
              />
              <ConfirmAction
                title={KPI_TEXT.cancelConfirm}
                onConfirm={() => cancel.mutate(row.kpiTargetId)}
              >
                <IconAction
                  title={KPI_TEXT.cancel}
                  icon={<CloseCircleOutlined />}
                  danger
                  loading={cancel.isPending && cancel.variables === row.kpiTargetId}
                />
              </ConfirmAction>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          style={{ width: 260 }}
          defaultValue={filter.keyword}
          onSearch={(keyword) =>
            onFilterChange({ ...filter, page: 1, keyword: keyword || undefined })
          }
        />
        <FilterSelect<KpiTargetStatus>
          placeholder={KPI_TEXT.filter.statusPlaceholder}
          style={{ width: 180 }}
          value={filter.status}
          options={STATUS_OPTIONS}
          onChange={(status) => onFilterChange({ ...filter, page: 1, status })}
        />
        {onAdd && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onAdd}>
            {KPI_TEXT.add}
          </Button>
        )}
      </Space>

      <QueryError error={targets.error} onRetry={() => void targets.refetch()} />
      <FinishOrderModal target={finishing} onClose={() => setFinishing(null)} />
      <KpiCalendarModal target={calendarOf} onClose={() => setCalendarOf(null)} />

      <DataTable<KpiTarget>
        rowKey="kpiTargetId"
        size="small"
        columns={columns}
        dataSource={targets.data?.items}
        loading={targets.isFetching}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: targets.data?.page ?? filter.page,
          pageSize: targets.data?.pageSize ?? filter.pageSize,
          total: targets.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => onFilterChange({ ...filter, page, pageSize }),
        }}
      />
    </Space>
  );
}
