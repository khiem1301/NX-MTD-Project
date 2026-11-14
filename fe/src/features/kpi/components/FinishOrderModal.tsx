import { Alert, Descriptions, Modal, Spin, Tag } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useFinishKpiTarget, useFinishPreview } from '../hooks/useKpi';
import type { KpiTarget } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MismatchDetail } from './MismatchDetail';

type Props = { target: KpiTarget | null; onClose: () => void };

const text = KPI_TEXT.finish;

/**
 * Kết thúc đơn của tổ (Quản đốc): xem trước kết quả KPI, gợi ý tăng ca (chỉ thông báo, không tạo đơn) và cảnh báo lệch kiểm cuối đơn theo
 * công đoạn → dòng đơn → công nhân đã báo (sản lượng của họ). Hệ thống không tự trừ lương, chỉ thông báo Quản đốc và tổ trưởng để xác
 * minh (DEV-ALL-107).
 */
export function FinishOrderModal({ target, onClose }: Props) {
  const preview = useFinishPreview(target?.kpiTargetId ?? null);
  const finish = useFinishKpiTarget();
  const data = preview.data;

  const otMessage =
    data?.otDecision === 'AutoApproved'
      ? text.otAuto.replace('{pct}', String(data.thresholdPct))
      : data?.otDecision === 'LeaderDecides'
        ? text.otLeader.replace('{pct}', String(data.thresholdPct))
        : null;

  return (
    <Modal
      open={target !== null}
      width={MODAL_WIDTH.md}
      title={target ? `${text.title}: ${target.orderCode} · ${target.sewingTeamName}` : text.title}
      okText={text.confirm}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !data || data.blockers.length > 0, danger: true }}
      confirmLoading={finish.isPending}
      onCancel={onClose}
      onOk={() => target && finish.mutate(target.kpiTargetId, { onSuccess: onClose })}
      destroyOnHidden
    >
      {preview.isLoading && <Spin />}
      <QueryError error={preview.error} onRetry={() => void preview.refetch()} />
      {data && (
        <>
          <Alert type="info" showIcon message={text.hint} style={{ marginBottom: 12 }} />
          <Descriptions size="small" bordered column={2} style={{ marginBottom: 12 }}>
            <Descriptions.Item label={text.target}>{formatQty(data.targetQty)}</Descriptions.Item>
            <Descriptions.Item label={text.actual}>{formatQty(data.actualQty)}</Descriptions.Item>
            <Descriptions.Item label={text.result}>
              <Tag color={data.achieved ? 'green' : 'orange'}>
                {data.achieved ? text.achieved : text.notAchieved}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item
              label={text.shortfall}
            >{`${formatQty(data.shortfallQty)} (${formatQty(data.shortfallPct)}%)`}</Descriptions.Item>
          </Descriptions>
          {otMessage && (
            <Alert type="warning" showIcon message={otMessage} style={{ marginBottom: 12 }} />
          )}
          {data.blockers.map((b) => (
            <Alert key={b} type="error" showIcon message={b} style={{ marginBottom: 12 }} />
          ))}
          <MismatchDetail operations={data.operations} emptyText={text.noMismatch} />
        </>
      )}
    </Modal>
  );
}
