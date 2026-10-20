import { Card } from 'antd';
import { TeamHoldingsTable } from '../components/TeamHoldingsTable';
import { LABELS } from '../constants';

/** Tổ trưởng / tổ phó may xem BTP tổ mình đang giữ (đã nhận từ kho − đã giao CN + đã thu hồi). */
export function PieceHoldingsPage() {
  return (
    <Card title={LABELS.pieceHoldingsTitle}>
      <TeamHoldingsTable />
    </Card>
  );
}
