import { Card, Col, Row, Statistic, Typography, theme } from 'antd';
import { fill, formatCurrency, formatDateTime } from '@/utils/format';
import { BREAK_EVEN_TEXT } from '../constants';
import type { BreakEvenSummary } from '../utils/pivotResults';

const T = BREAK_EVEN_TEXT.summary;

/** Tổng quan lần tính mới nhất: doanh thu, biến phí, định phí, thặng dư theo hai cách, số mã nguy cơ lỗ. */
export function BreakEvenSummaryCard({ summary: s }: { summary: BreakEvenSummary }) {
  const { token } = theme.useToken();
  const money = (v: number) => formatCurrency(v);
  const surplusColor = (v: number) => (v < 0 ? token.colorError : token.colorSuccess);
  return (
    <Card size="small" title={fill(T.title, formatDateTime(s.calculatedAt))}>
      <Row gutter={[16, 12]}>
        <Col xs={12} md={6}>
          <Statistic title={T.revenue} value={s.revenue} formatter={(v) => money(Number(v))} />
        </Col>
        <Col xs={12} md={6}>
          <Statistic
            title={T.variableCost}
            value={s.variableCost}
            formatter={(v) => money(Number(v))}
          />
        </Col>
        <Col xs={12} md={6}>
          <Statistic title={T.fixedCost} value={s.fixedCost} formatter={(v) => money(Number(v))} />
        </Col>
        <Col xs={12} md={6}>
          <Typography.Text type="secondary">{T.surplus}</Typography.Text>
          <div>
            {BREAK_EVEN_TEXT.basis.Output}:{' '}
            <strong style={{ color: surplusColor(s.surplusOutput) }}>
              {money(s.surplusOutput)}
            </strong>
          </div>
          <div>
            {BREAK_EVEN_TEXT.basis.LabourHours}:{' '}
            <strong style={{ color: surplusColor(s.surplusLabour) }}>
              {money(s.surplusLabour)}
            </strong>
          </div>
        </Col>
      </Row>
      <Typography.Text
        type={s.riskOutput + s.riskLabour > 0 ? 'warning' : 'success'}
        style={{ display: 'block', marginTop: 8 }}
      >
        {s.riskOutput + s.riskLabour > 0 ? fill(T.risk, s.riskOutput, s.riskLabour) : T.noRisk}
      </Typography.Text>
    </Card>
  );
}
