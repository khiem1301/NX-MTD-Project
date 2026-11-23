import { Col, Row, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import {
  ATTENDANCE_ROUTES,
  MONTHLY_COST_ROUTES,
  PRODUCTION_ROUTES,
  PURCHASE_ROUTES,
} from '@/constants/routes';
import { fill } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import { useAccountantHome } from '../../hooks/useHome';
import type { AccountantHome as AccountantHomeData, HomeItem } from '../../types';
import { HomeItemList } from './HomeItemList';
import { HomeState } from './HomeState';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Hai dòng tình trạng tháng: chi phí đã nhập chưa và trạng thái bảng lương. */
const monthStatus = (d: AccountantHomeData): HomeItem[] => {
  const payroll = d.payrollStatus
    ? T.payrollStatus[d.payrollStatus as keyof typeof T.payrollStatus]
    : undefined;
  return [
    {
      title: T.costEntered,
      meta: null,
      tag: d.costEntered ? T.entered : T.notEntered,
      tagColor: d.costEntered ? 'green' : 'orange',
    },
    {
      title: T.payroll,
      meta: null,
      tag: payroll?.text ?? T.noPayroll,
      tagColor: payroll?.color ?? 'gray',
    },
  ];
};

/** Trang chủ Kế toán: việc của tháng, thanh toán và giá trị đơn, tình trạng chi phí / lương. */
export function AccountantHome() {
  const query = useAccountantHome();
  const navigate = useNavigate();
  return (
    <HomeState query={query}>
      {(d) => (
        <>
          <SectionLabel>{T.month}</SectionLabel>
          <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.missingPunch}
                value={d.missingPunch}
                tone={d.missingPunch > 0 ? 'danger' : 'default'}
                to={ATTENDANCE_ROUTES.attendance}
              />
            </Col>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.pieceRates}
                value={d.pieceRatesMissing + d.pieceRatesRejected}
                tone={d.pieceRatesMissing + d.pieceRatesRejected > 0 ? 'warn' : 'default'}
                note={
                  <Typography.Text type="secondary">
                    {fill(T.tiles.pieceRatesNote, d.pieceRatesMissing, d.pieceRatesRejected)}
                  </Typography.Text>
                }
                to={PRODUCTION_ROUTES.operations}
              />
            </Col>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.purchasesUnpaid}
                value={d.purchasesUnpaid}
                tone={d.purchasesUnpaid > 0 ? 'warn' : 'default'}
                to={PURCHASE_ROUTES.payments}
              />
            </Col>
          </Row>
          <Row gutter={[12, 12]}>
            <Col xs={24} lg={14}>
              <HomeItemList
                title={T.cards.paymentsAndOrders}
                items={[...d.orders, ...d.purchases]}
                onOpen={() => navigate(PURCHASE_ROUTES.payments)}
              />
            </Col>
            <Col xs={24} lg={10}>
              <HomeItemList
                title={T.cards.monthStatus}
                items={monthStatus(d)}
                onOpen={() => navigate(MONTHLY_COST_ROUTES.accountant)}
              />
            </Col>
          </Row>
        </>
      )}
    </HomeState>
  );
}
