import { Card, Tabs } from 'antd';
import { VL_LABELS } from '../constants';
import { TripsTab } from '../components/TripsTab';
import { VehiclesTab } from '../components/VehiclesTab';

/** UC35: Kế toán ghi sổ chuyến xe và nhiên liệu; cảnh báo lệch km / xăng vượt định mức. */
export function VehicleLogPage() {
  return (
    <Card title={VL_LABELS.title}>
      <Tabs
        items={[
          { key: 'trips', label: VL_LABELS.tripsTab, children: <TripsTab /> },
          { key: 'vehicles', label: VL_LABELS.vehiclesTab, children: <VehiclesTab /> },
        ]}
      />
    </Card>
  );
}
