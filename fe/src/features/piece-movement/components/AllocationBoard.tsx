import { useMemo, useState } from 'react';
import { SearchOutlined, RollbackOutlined, EyeOutlined, SendOutlined } from '@ant-design/icons';
import { Alert, Input, Space, Tag, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { normalizeText } from '@/utils/text';
import { PM_LABELS } from '../constants';
import { useBoard } from '../hooks/usePieceMovement';
import type { OperationProgress, WorkerAllocationLine } from '../types';
import { MovementFormModal, type AllocationTarget } from './MovementFormModal';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

/**
 * Bảng chia việc của tổ may: mỗi dòng là một công đoạn × màu × size với số cần làm (= số bộ BTP tổ đã nhận), đã giao, chờ xác nhận và còn chưa giao.
 * Bấm Giao để chia cho công nhân; mở rộng dòng để thấy ai đang giữ việc và thu hồi. Tìm được theo tên công đoạn to.
 */
export function AllocationBoard({ canEdit }: { canEdit: boolean }) {
  const [orderId, setOrderId] = useState<number>();
  const [keyword, setKeyword] = useState('');
  const [target, setTarget] = useState<AllocationTarget | null>(null);
  const [viewing, setViewing] = useState<OperationProgress | null>(null);
  const board = useBoard(orderId);

  const data = board.data;
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
        normalizeText(`${o.operationCode} ${o.operationName} ${o.groupName ?? ''}`).includes(
          needle,
        ),
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
          <Typography.Text>{`${o.operationCode} - ${o.operationName}`}</Typography.Text>
          <Space size={4}>{o.groupName && <Tag color="geekblue">{o.groupName}</Tag>}</Space>
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
      render: (_, o) => (
        <Space size={0}>
          <IconAction
            title={MESSAGES.common.view}
            icon={<EyeOutlined />}
            onClick={() => setViewing(o)}
          />
          {canEdit && (
            <IconAction
              title={PM_LABELS.giveAction}
              icon={<SendOutlined />}
              disabled={o.unallocatedQty <= 0}
              onClick={() => setTarget({ kind: 'HANDOVER', progress: o })}
            />
          )}
        </Space>
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
      {orderId !== undefined && !board.isLoading && data && data.operations.length === 0 && (
        <Alert type="warning" showIcon message={PM_LABELS.noBoard} />
      )}
      <DataTable
        rowKey={(o) => `${o.orderLineId}-${o.operationId}`}
        columns={columns}
        dataSource={rows}
        loading={board.isFetching}
      />
      <DetailModal
        open={viewing !== null}
        onClose={() => setViewing(null)}
        title={viewing ? `${viewing.operationCode} - ${viewing.operationName}` : undefined}
      >
        {viewing && (
          <DataTable
            pagination={false}
            rowKey="workerId"
            dataSource={workersByRow.get(`${viewing.orderLineId}-${viewing.operationId}`)}
            columns={[
              {
                title: PM_LABELS.workersHolding,
                key: 'worker',
                render: (_: unknown, w: WorkerAllocationLine) =>
                  `${w.workerCode} - ${w.workerName}`,
              },
              { title: PM_LABELS.held, dataIndex: 'heldQty', align: 'right' as const },
              {
                title: PM_LABELS.pending,
                dataIndex: 'pendingRetrievalQty',
                align: 'right' as const,
              },
              { title: PM_LABELS.output, dataIndex: 'outputQty', align: 'right' as const },
              {
                title: PM_LABELS.retrievable,
                dataIndex: 'retrievableQty',
                align: 'right' as const,
              },
              {
                title: MESSAGES.common.actions,
                key: 'action',
                render: (_: unknown, w: WorkerAllocationLine) =>
                  canEdit && (
                    <IconAction
                      title={PM_LABELS.retrieveAction}
                      icon={<RollbackOutlined />}
                      disabled={w.retrievableQty <= 0}
                      onClick={() => setTarget({ kind: 'RETRIEVAL', progress: viewing, worker: w })}
                    />
                  ),
              },
            ]}
          />
        )}
      </DetailModal>
      <MovementFormModal
        key={
          target
            ? `${target.kind}-${target.progress.orderLineId}-${target.progress.operationId}`
            : 'none'
        }
        target={target}
        onClose={() => setTarget(null)}
      />
    </Space>
  );
}
