import { useState } from 'react';
import { Alert, Card, Col, Row, Select, Space, Statistic, Table, Tag, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { BTP_TEXT } from '../constants';
import { useKpiBtpComparison, useKpiBtpOptions } from '../hooks/useKpiBtp';
import type { KpiBtpDay, KpiBtpOption } from '../types';

const text = BTP_TEXT;
const optionLabel = (o: KpiBtpOption) =>
  `${o.orderCode} · ${o.productCode} · ${o.sewingTeamName} · ${formatDate(o.startDate)} - ${formatDate(o.endDate)}`;

/**
 * So sánh BTP đã cắt vào kho – KPI ngày đã đặt cho tổ may – số hoàn thiện thực tế của tổ (QA/QC), trừ dần BTP theo lũy kế.
 * Quản đốc, Admin, Thủ kho xem mọi tổ; tổ trưởng / tổ phó chỉ thấy KPI của tổ mình. Chỉ xem.
 */
export function KpiBtpComparePage() {
  const options = useKpiBtpOptions();
  const [picked, setPicked] = useState<number | null>(null);
  // Mặc định mở KPI mới nhất; chọn tay thì theo lựa chọn.
  const kpiTargetId = picked ?? options.data?.[0]?.kpiTargetId ?? null;
  const comparison = useKpiBtpComparison(kpiTargetId);
  const data = comparison.data;

  const columns: TableColumnsType<KpiBtpDay> = [
    { title: text.col.date, dataIndex: 'workDate', fixed: 'left', render: (v: string, r) => (
      <Space size={4}>{formatDate(v)}{r.isFuture && <Tag>{text.forecast}</Tag>}</Space>
    ) },
    { title: text.col.dailyTarget, dataIndex: 'dailyTarget', align: 'right', render: formatQty },
    { title: text.col.cumTarget, dataIndex: 'cumTarget', align: 'right', render: formatQty },
    { title: text.col.cut, dataIndex: 'cutQty', align: 'right', render: (v: number) => (v ? formatQty(v) : '—') },
    { title: text.col.cumCut, dataIndex: 'cumCut', align: 'right', render: formatQty },
    { title: text.col.actual, dataIndex: 'actualQty', align: 'right', render: (v: number | null) => (v === null ? '—' : formatQty(v)) },
    { title: text.col.cumActual, dataIndex: 'cumActual', align: 'right', render: formatQty },
    {
      title: text.col.remaining,
      dataIndex: 'btpRemaining',
      align: 'right',
      render: (v: number, r) => <Typography.Text strong type={r.lowBtp || v < 0 ? 'danger' : undefined}>{formatQty(v)}</Typography.Text>,
    },
    { title: text.col.shortfall, dataIndex: 'cutShortfall', align: 'right', render: (v: number) => (v > 0 ? formatQty(v) : '—') },
    {
      title: text.col.warning,
      key: 'warning',
      render: (_, r) => (
        <Space size={4} wrap>
          {r.lowBtp && <Tag color="red">{text.lowBtp}</Tag>}
          {r.behindTarget && <Tag color="orange">{text.behind}</Tag>}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>{text.title}</Typography.Title>
        <Typography.Text type="secondary">{text.subtitle}</Typography.Text>
      </div>

      <QueryError error={options.error} onRetry={() => void options.refetch()} />
      <Select
        showSearch
        optionFilterProp="label"
        placeholder={text.pick}
        loading={options.isLoading}
        value={kpiTargetId ?? undefined}
        onChange={setPicked}
        options={(options.data ?? []).map((o) => ({ value: o.kpiTargetId, label: optionLabel(o) }))}
        style={{ width: '100%', maxWidth: 720 }}
        notFoundContent={text.noKpi}
      />

      <QueryError error={comparison.error} onRetry={() => void comparison.refetch()} />
      {data && (
        <>
          <Row gutter={[16, 16]}>
            <Col xs={12} md={4}><Card size="small"><Statistic title={text.sum.plan} value={data.summary.planTotal} /></Card></Col>
            <Col xs={12} md={4}><Card size="small"><Statistic title={text.sum.opening} value={data.summary.openingBtp} /></Card></Col>
            <Col xs={12} md={4}><Card size="small"><Statistic title={text.sum.cut} value={data.summary.cutTotal} /></Card></Col>
            <Col xs={12} md={4}><Card size="small"><Statistic title={text.sum.actual} value={data.summary.actualTotal} /></Card></Col>
            <Col xs={12} md={4}>
              <Card size="small">
                <Statistic title={text.sum.remaining} value={data.summary.btpRemainingNow} valueStyle={data.summary.btpRemainingNow < 0 ? { color: '#cf1322' } : undefined} />
              </Card>
            </Col>
            <Col xs={12} md={4}>
              <Card size="small">
                <Statistic title={text.sum.shortfall} value={data.summary.cutShortfall} valueStyle={data.summary.cutShortfall > 0 ? { color: '#d46b08' } : undefined} />
              </Card>
            </Col>
          </Row>
          {data.summary.cutShortfall > 0 && (
            <Alert type="warning" showIcon message={`${text.needCut} ${formatQty(data.summary.cutShortfall)}`} />
          )}
          <Table<KpiBtpDay>
            rowKey="workDate"
            size="small"
            columns={columns}
            dataSource={data.days}
            loading={comparison.isFetching}
            pagination={false}
            locale={{ emptyText: MESSAGES.common.empty }}
            scroll={{ x: 'max-content' }}
            rowClassName={(r) => (r.isFuture ? 'kpi-btp-future' : '')}
            onRow={(r) => (r.isFuture ? { style: { opacity: 0.65 } } : {})}
          />
          <Typography.Text type="secondary">{text.formula}</Typography.Text>
        </>
      )}
    </Space>
  );
}
