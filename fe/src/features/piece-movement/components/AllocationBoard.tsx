import { useMemo, useState } from 'react';
import { SearchOutlined } from '@ant-design/icons';
import { Alert, Button, Input, Space, Table, Tag, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { normalizeText } from '@/utils/text';
import { PM_LABELS } from '../constants';
import { useBoard } from '../hooks/usePieceMovement';
import type { OperationProgress, WorkerAllocationLine } from '../types';
import { MovementFormModal, type AllocationTarget } from './MovementFormModal';

/**
 * Bảng chia việc của tổ may: mỗi dòng là một công đoạn × màu × size với số cần làm, đã giao, chờ xác nhận và còn chưa giao.
 * Bấm Giao để chia cho công nhân; mở rộng dòng để thấy ai đang giữ việc và thu hồi. Tìm được theo tên công đoạn to.
 */
export function AllocationBoard({ canEdit }: { canEdit: boolean }) {
  const [orderId, setOrderId] = useState<number>();
  const [keyword, setKeyword] = useState('');
  const [target, setTarget] = useState<AllocationTarget | null>(null);
  const board = useBoard(orderId);

  const data = board.data;
  const pieceByLine = useMemo(
    () => new Map((data?.team ?? []).map((t) => [t.orderLineId, t.availablePieceQty])),
    [data?.team],
  );
  const workersByRow = useMemo(() => {
    const map = new Map<string, WorkerAllocationLine[]>();
    for (const w of data?.workers ?? []) {
      const key = `${w.orderLineId}-${w.operationId}`;
      map.set(key, [...(map.get(key) ?? []), w]);
    }
    return map;
  }, [data?.workers]);

  const rows = useMemo(() => {
    const needle = normalizeText(keyword.trim());
    return (data?.operations ?? []).filter(
      (o) =>
        needle === '' ||
        normalizeText(`${o.operationCode} ${o.operationName} ${o.groupName ?? ''}`).includes(needle),
    );
  }, [data?.operations, keyword]);

  const columns: TableColumnsType<OperationProgress> = [
    {
      title: PM_LABELS.orderLine,
      key: 'line',
      render: (_, o) => `${o.productCode} ${o.color}-${o.sizeCode}`,
    },
    {
      title: PM_LABELS.operation,
      key: 'operation',
      render: (_, o) => (
        <Space direction="vertical" size={0}>
          <span>{`${o.operationCode} - ${o.operationName}`}</span>
          <Space size={4}>
            {o.groupName && <Tag color="geekblue">{o.groupName}</Tag>}
            <Tag color={o.usesSemiFinished ? 'blue' : 'default'}>
              {o.usesSemiFinished ? PM_LABELS.withPieces : PM_LABELS.withoutPieces}
            </Tag>
          </Space>
        </Space>
      ),
    },
    { title: PM_LABELS.required, dataIndex: 'requiredQty', align: 'right' },
    { title: PM_LABELS.allocated, dataIndex: 'allocatedQty', align: 'right' },
    { title: PM_LABELS.pending, dataIndex: 'pendingQty', align: 'right' },
    {
      title: PM_LABELS.unallocated,
      dataIndex: 'unallocatedQty',
      align: 'right',
      render: (v: number) => <Typography.Text strong={v > 0}>{v}</Typography.Text>,
    },
    { title: PM_LABELS.output, dataIndex: 'outputQty', align: 'right' },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, o) =>
        canEdit && (
          <Button
            type="primary"
            size="small"
            disabled={o.unallocatedQty <= 0}
            onClick={() =>
              setTarget({
                kind: 'HANDOVER',
                progress: o,
                pieceLimit: pieceByLine.get(o.orderLineId) ?? 0,
              })
            }
          >
            {PM_LABELS.giveAction}
          </Button>
        ),
    },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <OrderSelect
          value={orderId}
          onChange={setOrderId}
          placeholder={PM_LABELS.selectOrder}
          width={300}
        />
        <Input
          allowClear
          prefix={<SearchOutlined />}
          placeholder={PM_LABELS.searchOperation}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          style={{ width: 300 }}
        />
      </Space>
      <QueryError error={board.error} onRetry={() => void board.refetch()} />
      {orderId !== undefined && data && data.team.length > 0 && (
        <Alert
          type="info"
          showIcon
          message={PM_LABELS.teamPieces}
          description={
            <Space direction="vertical" size={2}>
              {data.team.map((t) => (
                <span key={t.orderLineId}>
                  {`${t.productCode} ${t.color}-${t.sizeCode}: ${PM_LABELS.held} ${t.heldByTeam}, ${PM_LABELS.pending.toLowerCase()} ${t.pendingPieceQty}, ${PM_LABELS.available.toLowerCase()} ${t.availablePieceQty}`}
                </span>
              ))}
              <Typography.Text type="secondary">{PM_LABELS.teamPiecesHint}</Typography.Text>
            </Space>
          }
        />
      )}
      {orderId !== undefined && !board.isLoading && data && data.operations.length === 0 && (
        <Alert type="warning" showIcon message={PM_LABELS.noBoard} />
      )}
      <Table
        rowKey={(o) => `${o.orderLineId}-${o.operationId}`}
        columns={columns}
        dataSource={rows}
        loading={board.isFetching}
        pagination={{ pageSize: 50, hideOnSinglePage: true }}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        expandable={{
          rowExpandable: (o) => workersByRow.has(`${o.orderLineId}-${o.operationId}`),
          expandedRowRender: (o) => (
            <Table
              size="small"
              pagination={false}
              rowKey="workerId"
              dataSource={workersByRow.get(`${o.orderLineId}-${o.operationId}`)}
              columns={[
                {
                  title: PM_LABELS.workersHolding,
                  key: 'worker',
                  render: (_: unknown, w: WorkerAllocationLine) => `${w.workerCode} - ${w.workerName}`,
                },
                { title: PM_LABELS.held, dataIndex: 'heldQty', align: 'right' as const },
                { title: PM_LABELS.pending, dataIndex: 'pendingRetrievalQty', align: 'right' as const },
                { title: PM_LABELS.output, dataIndex: 'outputQty', align: 'right' as const },
                { title: PM_LABELS.retrievable, dataIndex: 'retrievableQty', align: 'right' as const },
                {
                  title: MESSAGES.common.actions,
                  key: 'action',
                  render: (_: unknown, w: WorkerAllocationLine) =>
                    canEdit && (
                      <Button
                        size="small"
                        disabled={w.retrievableQty <= 0}
                        onClick={() => setTarget({ kind: 'RETRIEVAL', progress: o, worker: w })}
                      >
                        {PM_LABELS.retrieveAction}
                      </Button>
                    ),
                },
              ]}
            />
          ),
        }}
      />
      <MovementFormModal
        key={target ? `${target.kind}-${target.progress.orderLineId}-${target.progress.operationId}` : 'none'}
        target={target}
        onClose={() => setTarget(null)}
      />
    </Space>
  );
}
