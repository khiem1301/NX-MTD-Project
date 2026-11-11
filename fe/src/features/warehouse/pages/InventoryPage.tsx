import { Card, Tabs } from 'antd';
import { BalanceTable } from '../components/BalanceTable';
import { NxtReportPanel } from '../components/NxtReportPanel';
import { TeamHoldingsTable } from '../components/TeamHoldingsTable';
import { LABELS, WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '../constants';

const HOLDINGS_TAB = 'TEAM_HOLDINGS';
const NXT_REPORT_TAB = 'NXT_REPORT';

/** UC24: tồn 3 kho (bấm dòng xem thẻ kho), BTP các tổ may đang giữ và báo cáo nhập xuất tồn theo đơn, kỳ. */
export function InventoryPage() {
  return (
    <Card title={LABELS.inventoryTitle}>
      <Tabs
        destroyOnHidden
        items={[
          ...Object.values(WAREHOUSE_TYPE).map((type) => ({
            key: type,
            label: WAREHOUSE_TYPE_LABEL[type],
            children: <BalanceTable warehouseType={type} />,
          })),
          { key: HOLDINGS_TAB, label: LABELS.teamHoldings, children: <TeamHoldingsTable /> },
          { key: NXT_REPORT_TAB, label: LABELS.nxtReportTitle, children: <NxtReportPanel /> },
        ]}
      />
    </Card>
  );
}
