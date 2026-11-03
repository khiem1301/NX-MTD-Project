import { Card, Tabs } from 'antd';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { AllocationHistory } from '../components/AllocationHistory';
import { DailyAllocationBoard } from '../components/DailyAllocationBoard';
import { PM_LABELS } from '../constants';

/** UC53 / UC54: tổ trưởng / tổ phó may giao sản lượng công đoạn theo ngày cho công nhân, thu hồi phần chưa làm hết cuối ngày. */
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
            children: <DailyAllocationBoard canEdit={canEdit} />,
          },
          { key: 'history', label: PM_LABELS.historyTab, children: <AllocationHistory /> },
        ]}
      />
    </Card>
  );
}
