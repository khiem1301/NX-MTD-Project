import { Card, Tabs } from 'antd';
import { BalanceTable } from '../components/BalanceTable';
import { TeamHoldingsTable } from '../components/TeamHoldingsTable';
import { LABELS, WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '../constants';

const HOLDINGS_TAB = 'TEAM_HOLDINGS';

/** UC24: tồn 3 kho (bấm dòng xem thẻ kho) và BTP các tổ may đang giữ. */
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
        ]}
      />
    </Card>
  );
}
