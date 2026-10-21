import { Alert, Button, Card, Col, Descriptions, Row, Statistic, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { COST_SUMMARY_TEXT, costCategoryLabel } from '../constants';
import { useExportCostSummary } from '../hooks/useCostSummary';
import type { CostByProduct, CostFixedItem, CostSummary, CostSummaryFilter } from '../types';
import { formatCurrency } from '../utils';

type Props = { summary: CostSummary; filter: CostSummaryFilter };

const fixedColumns: ColumnsType<CostFixedItem> = [
  {
    title: COST_SUMMARY_TEXT.fixedTable.category,
    dataIndex: 'costCategoryCode',
    render: (v: string) => costCategoryLabel(v),
  },
  {
    title: COST_SUMMARY_TEXT.fixedTable.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
];

const byProductColumns: ColumnsType<CostByProduct> = [
  {
    title: COST_SUMMARY_TEXT.byProductTable.product,
    key: 'product',
    render: (_, row) => `${row.productCode} — ${row.productName}`,
  },
  {
    title: COST_SUMMARY_TEXT.byProductTable.amount,
    dataIndex: 'amount',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
];

/** UC12 — tổng hợp chi phí (vật tư + lương) của tháng hiện tại: tổng theo khoản cố định, tổng theo mã
 * hàng cho khoản biến đổi, lương bảng lương đã duyệt, và tổng cộng. CHỈ Admin (GBR-02). */
export function CostSummaryPanel({ summary, filter }: Props) {
  const exportCostSummary = useExportCostSummary();
  const reconciliation = summary.mealsReconciliation;

  return (
    <Card
      size="small"
      title={`${COST_SUMMARY_TEXT.periodMonth} ${summary.periodMonth}`}
      extra={
        <Button
          icon={<DownloadOutlined />}
          loading={exportCostSummary.isPending}
          onClick={() => exportCostSummary.mutate(filter)}
        >
          {COST_SUMMARY_TEXT.export}
        </Button>
      }
    >
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}>
          <Statistic title={COST_SUMMARY_TEXT.summary.fixedCostTotal} value={formatCurrency(summary.fixedCostTotal)} />
        </Col>
        <Col xs={12} md={6}>
          <Statistic
            title={COST_SUMMARY_TEXT.summary.variableCostTotal}
            value={formatCurrency(summary.variableCostTotal)}
          />
        </Col>
        <Col xs={12} md={6}>
          <Statistic
            title={COST_SUMMARY_TEXT.summary.payrollTotal}
            value={summary.payrollApproved ? formatCurrency(summary.payrollTotal) : MESSAGES.costSummary.payrollNotApproved}
          />
        </Col>
        <Col xs={12} md={6}>
          <Statistic title={COST_SUMMARY_TEXT.summary.grandTotal} value={formatCurrency(summary.grandTotal)} />
        </Col>
      </Row>

      {summary.isLocked ? (
        <Descriptions size="small" style={{ marginBottom: 16 }}>
          <Descriptions.Item label={COST_SUMMARY_TEXT.summary.locked}>
            <Tag color="red">{COST_SUMMARY_TEXT.summary.locked}</Tag>
          </Descriptions.Item>
        </Descriptions>
      ) : null}

      {reconciliation.importedAmount !== null ? (
        <Alert
          type={reconciliation.hasDiscrepancy ? 'warning' : 'success'}
          showIcon
          style={{ marginBottom: 16 }}
          message={COST_SUMMARY_TEXT.reconciliation.title}
          description={`${COST_SUMMARY_TEXT.reconciliation.expected}: ${formatCurrency(reconciliation.expectedAmount)} — ${COST_SUMMARY_TEXT.reconciliation.imported}: ${formatCurrency(reconciliation.importedAmount)}. ${reconciliation.hasDiscrepancy ? COST_SUMMARY_TEXT.reconciliation.mismatch : COST_SUMMARY_TEXT.reconciliation.match}`}
        />
      ) : null}

      <Row gutter={16}>
        <Col xs={24} md={12}>
          <Table<CostFixedItem>
            rowKey="costCategoryCode"
            size="small"
            title={() => COST_SUMMARY_TEXT.summary.fixedCostTotal}
            columns={fixedColumns}
            dataSource={summary.fixedCosts}
            pagination={false}
            locale={{ emptyText: MESSAGES.common.empty }}
          />
        </Col>
        <Col xs={24} md={12}>
          <Table<CostByProduct>
            rowKey="productId"
            size="small"
            title={() => COST_SUMMARY_TEXT.summary.variableCostTotal}
            columns={byProductColumns}
            dataSource={summary.variableCostsByProduct}
            pagination={false}
            locale={{ emptyText: MESSAGES.common.empty }}
          />
        </Col>
      </Row>
    </Card>
  );
}
