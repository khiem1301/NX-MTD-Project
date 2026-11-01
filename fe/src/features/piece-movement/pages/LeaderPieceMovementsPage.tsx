import { Card, Tabs } from 'antd';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { AllocationBoard } from '../components/AllocationBoard';
import { PM_LABELS } from '../constants';
import { AllocationHistory } from '../components/AllocationHistory';

/** UC53 / UC54: tổ trưởng / tổ phó may chia việc cho công nhân theo công đoạn, xác nhận hộ khi công nhân chưa xác nhận. */
export function LeaderPieceMovementsPage() {
  const { user } = useAuth();
  const canEdit = user?.role === ROLES.TeamLeader || user?.role === ROLES.TeamDeputy;
  return (
    <Card title={PM_LABELS.leaderTitle}>
      <Tabs
        items={[
          {
            key: 'board',
            label: PM_LABELS.boardTab,
            children: <AllocationBoard canEdit={canEdit} />,
          },
          { key: 'history', label: PM_LABELS.historyTab, children: <AllocationHistory /> },
        ]}
      />
    </Card>
  );
}
