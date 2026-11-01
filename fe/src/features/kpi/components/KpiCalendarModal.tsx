import { Modal, Skeleton } from 'antd';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QueryError } from '@/components/common/QueryError';
import { fill, formatDate } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useKpiCalendar } from '../hooks/useKpi';
import type { KpiRole, KpiTarget } from '../types';
import { KpiCalendarBody } from './KpiCalendarBody';

type KpiCalendarModalProps = {
  target: KpiTarget | null;
  role: KpiRole;
  onClose: () => void;
};

/** Modal lịch KPI theo ngày của một chỉ tiêu. */
export function KpiCalendarModal({ target, role, onClose }: KpiCalendarModalProps) {
  const calendar = useKpiCalendar(target?.kpiTargetId ?? null, role);
  const data = calendar.data;

  return (
    <Modal
      open={target !== null}
      centered
      width={MODAL_WIDTH.lg}
      styles={{ body: { maxHeight: 'calc(100vh - 120px)', overflowY: 'auto' } }}
      footer={null}
      destroyOnHidden
      onCancel={onClose}
      title={
        target &&
        fill(
          KPI_TEXT.calendar.title,
          target.orderCode,
          target.productCode,
          target.sewingTeamName,
          formatDate(target.startDate),
          formatDate(target.endDate),
        )
      }
    >
      <QueryError error={calendar.error} onRetry={() => void calendar.refetch()} />
      {calendar.isLoading && <Skeleton active />}
      {data && <KpiCalendarBody data={data} editable={role === 'manager' && data.canEdit} />}
    </Modal>
  );
}
