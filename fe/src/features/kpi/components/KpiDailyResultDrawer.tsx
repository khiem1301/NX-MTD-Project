import { Alert, Descriptions, Drawer, Spin, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useKpiDailyResults, useKpiTargetDetail } from '../hooks/useKpi';
import type { KpiDailyResult } from '../types';

type Props = { kpiTargetId: number | null; onClose: () => void };

const formatPercent = (value: number | null): string => (value === null ? '—' : `${formatQty(value)}%`);

/** MI-18 "KPI theo ngày": chi tiết một chỉ tiêu KPI + bảng kết quả từng ngày (vw_KpiDailyResult). */
export function KpiDailyResultDrawer({ kpiTargetId, onClose }: Props) {
  const detail = useKpiTargetDetail(kpiTargetId);
  const dailyResults = useKpiDailyResults(kpiTargetId);

  const columns: ColumnsType<KpiDailyResult> = [
    { title: KPI_TEXT.dailyTable.workDate, dataIndex: 'workDate', render: formatDate },
    {
      title: KPI_TEXT.dailyTable.dailyTarget,
      dataIndex: 'dailyTarget',
      align: 'right',
      render: (v: number | null) => formatQty(v),
    },
    {
      title: KPI_TEXT.dailyTable.finishedQty,
      dataIndex: 'finishedQty',
      align: 'right',
      render: (v: number | null) => formatQty(v),
    },
    { title: KPI_TEXT.dailyTable.defectQty, dataIndex: 'defectQty', align: 'right' },
    {
      title: KPI_TEXT.dailyTable.defectRatePct,
      dataIndex: 'defectRatePct',
      align: 'right',
      render: (v: number | null) => formatPercent(v),
    },
    {
      title: KPI_TEXT.dailyTable.percentAchieved,
      dataIndex: 'percentAchieved',
      align: 'right',
      render: (v: number | null) => formatPercent(v),
    },
  ];

  const d = detail.data;

  return (
    <Drawer
      open={kpiTargetId !== null}
      onClose={onClose}
      width={840}
      title={d ? `${KPI_TEXT.detail.drawerTitlePrefix}: ${d.orderCode} - ${d.productCode}` : undefined}
    >
      {detail.isLoading || dailyResults.isLoading ? <Spin /> : null}
      {detail.error ? <Alert type="error" showIcon message={detail.error.message} /> : null}
      {dailyResults.error ? (
        <Alert
          type="error"
          showIcon
          message={dailyResults.error.message}
          style={{ marginTop: detail.error ? 8 : 0 }}
        />
      ) : null}

      {d ? (
        <>
          <Descriptions column={{ xs: 1, sm: 2 }} bordered size="small" style={{ marginBottom: 16 }}>
            <Descriptions.Item label={KPI_TEXT.detail.sewingTeam}>{d.sewingTeamName}</Descriptions.Item>
            <Descriptions.Item label={KPI_TEXT.detail.period}>
              {formatDate(d.startDate)} - {formatDate(d.endDate)}
            </Descriptions.Item>
            <Descriptions.Item label={KPI_TEXT.detail.targetFinishedQty}>
              {formatQty(d.targetFinishedQty)}
            </Descriptions.Item>
            <Descriptions.Item label={KPI_TEXT.detail.workingDays}>{d.workingDays}</Descriptions.Item>
            <Descriptions.Item label={KPI_TEXT.detail.dailyTarget}>
              {formatQty(d.dailyTarget)}
            </Descriptions.Item>
            <Descriptions.Item label={KPI_TEXT.table.status}>
              <Tag color={KPI_TEXT.statusLabel[d.status].color}>{KPI_TEXT.statusLabel[d.status].text}</Tag>
            </Descriptions.Item>
          </Descriptions>

          <Alert
            type="info"
            showIcon
            message={KPI_TEXT.detail.noDailyResultsHint}
            style={{ marginBottom: 16 }}
          />

          <Table<KpiDailyResult>
            rowKey="workDate"
            size="middle"
            columns={columns}
            dataSource={dailyResults.data ?? []}
            loading={dailyResults.isFetching}
            pagination={false}
            locale={{ emptyText: MESSAGES.common.empty }}
            scroll={{ x: 'max-content' }}
          />
        </>
      ) : null}
    </Drawer>
  );
}
