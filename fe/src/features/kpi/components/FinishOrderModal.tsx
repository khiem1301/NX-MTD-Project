import { Alert, Descriptions, Modal, Spin, Table, Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useFinishKpiTarget, useFinishPreview } from '../hooks/useKpi';
import type { EndOfOrderRow, KpiTarget } from '../types';

type Props = { target: KpiTarget | null; onClose: () => void };

const text = KPI_TEXT.finish;

/**
 * Kết thúc đơn của tổ (Quản đốc): xem trước lệch kiểm cuối đơn theo công nhân, kết quả KPI và quyết định tăng ca,
 * rồi xác nhận. Phần lệch được ghi thẳng thành phiếu hàng hỏng (trừ lương) gắn với từng công nhân.
 */
export function FinishOrderModal({ target, onClose }: Props) {
  const preview = useFinishPreview(target?.kpiTargetId ?? null);
  const finish = useFinishKpiTarget();
  const data = preview.data;
  const mismatchTotal = (data?.rows ?? []).reduce((sum, r) => sum + r.mismatchQty, 0);

  const columns: TableColumnsType<EndOfOrderRow> = [
    { title: text.worker, key: 'worker', render: (_, r) => `${r.workerCode} - ${r.workerName}` },
    { title: text.line, key: 'line', render: (_, r) => `${r.color} - ${r.sizeCode}` },
    { title: text.reported, dataIndex: 'reportedQty', align: 'right', render: (v: number) => formatQty(v) },
    {
      title: text.mismatch,
      dataIndex: 'mismatchQty',
      align: 'right',
      render: (v: number) => (v > 0 ? <Tag color="red">{formatQty(v)}</Tag> : <Tag color="green">0</Tag>),
    },
  ];

  const otMessage =
    data?.otDecision === 'AutoApproved'
      ? text.otAuto.replace('{pct}', String(data.thresholdPct))
      : data?.otDecision === 'LeaderDecides'
        ? text.otLeader.replace('{pct}', String(data.thresholdPct))
        : null;

  return (
    <Modal
      open={target !== null}
      width={720}
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
              <Tag color={data.achieved ? 'green' : 'orange'}>{data.achieved ? text.achieved : text.notAchieved}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={text.shortfall}>{`${formatQty(data.shortfallQty)} (${formatQty(data.shortfallPct)}%)`}</Descriptions.Item>
          </Descriptions>
          {otMessage && <Alert type="warning" showIcon message={otMessage} style={{ marginBottom: 12 }} />}
          {data.blockers.map((b) => (
            <Alert key={b} type="error" showIcon message={b} style={{ marginBottom: 12 }} />
          ))}
          <Table<EndOfOrderRow>
            rowKey={(r) => `${r.orderLineId}-${r.workerId}`}
            size="small"
            pagination={false}
            columns={columns}
            dataSource={data.rows}
            locale={{ emptyText: text.noOutput }}
            title={() => (mismatchTotal > 0 ? text.mismatchTitle.replace('{n}', formatQty(mismatchTotal)) : text.noMismatch)}
          />
        </>
      )}
    </Modal>
  );
}
