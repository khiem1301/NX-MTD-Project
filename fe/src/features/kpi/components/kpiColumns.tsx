import { Progress, Tag, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { fill, formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import type { KpiTarget, KpiTargetStatus } from '../types';
import { KpiLinesCell } from './KpiLinesCell';

export const KPI_STATUS_OPTIONS = (
  Object.entries(KPI_TEXT.statusLabel) as [KpiTargetStatus, { text: string }][]
).map(([value, { text }]) => ({ value, label: text }));

/** Chỉ tiêu đạt: xanh; trên 70%: đang tiến triển; còn lại: cảnh báo. Vượt 100% vẫn vẽ đầy thanh. */
const progressStatus = (percent: number): 'success' | 'normal' | 'exception' =>
  percent >= 100 ? 'success' : percent >= 70 ? 'normal' : 'exception';

/** Cột xem chỉ tiêu KPI dùng chung cho Quản đốc và tổ trưởng; tổ trưởng chỉ thấy tổ mình nên bỏ cột tổ may. */
export const kpiTargetColumns = (showTeam: boolean): ColumnsType<KpiTarget> => [
  { title: KPI_TEXT.table.order, dataIndex: 'orderCode' },
  {
    title: KPI_TEXT.table.product,
    dataIndex: 'productCode',
    render: (code: string, row) => <Tooltip title={row.productName}>{code}</Tooltip>,
  },
  ...(showTeam
    ? ([
        { title: KPI_TEXT.table.sewingTeam, dataIndex: 'sewingTeamName' },
      ] as ColumnsType<KpiTarget>)
    : []),
  {
    title: KPI_TEXT.table.lines,
    key: 'lines',
    render: (_, row) => <KpiLinesCell lines={row.lines} />,
  },
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
];
