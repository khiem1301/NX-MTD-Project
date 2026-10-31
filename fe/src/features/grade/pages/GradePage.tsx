import { Card, Tabs } from 'antd';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { GR_LABELS } from '../constants';
import { EvaluationsTab } from '../components/EvaluationsTab';
import { GradeSetupTab } from '../components/GradeSetupTab';

/** UC15 / UC16 / UC50: tổ trưởng đề xuất bậc; Admin / Quản đốc quyết định; Admin đặt mức tiền bậc. */
export function GradePage() {
  const { hasRole } = usePermission();
  return (
    <Card title={GR_LABELS.title}>
      <Tabs
        items={[
          { key: 'evaluations', label: GR_LABELS.evaluationsTab, children: <EvaluationsTab /> },
          ...(hasRole(ROLES.Admin)
            ? [{ key: 'setup', label: GR_LABELS.levelsTab, children: <GradeSetupTab /> }]
            : []),
        ]}
      />
    </Card>
  );
}
