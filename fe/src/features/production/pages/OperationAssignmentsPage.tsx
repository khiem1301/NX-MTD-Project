import { useState } from 'react';
import { Alert, Button, Empty, Segmented, Select, Space } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useAssignmentBoard, type AssignmentView } from '../hooks/useAssignmentBoard';
import type { AssignmentRow } from '../utils/assignmentBoard';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AssignmentBulkBar } from '../components/AssignmentBulkBar';
import { AssignmentByWorker } from '../components/AssignmentByWorker';
import { AssignmentFilterBar } from '../components/AssignmentFilterBar';
import { AssignmentProgress } from '../components/AssignmentProgress';
import { assignmentColumns } from '../components/assignmentColumns';
import { CopyModal } from '../components/CopyModal';

const text = PRODUCTION_TEXT.assignments;
/** Chiều cao thanh đầu trang (layout.css), để đầu bảng dính ngay dưới nó khi cuộn. */
const APP_HEADER_HEIGHT = 64;
/** Bề rộng tối thiểu của bảng: màn hẹp hơn thì cuộn ngang, ô công nhân không bị cắt trên màn rộng. */
const TABLE_MIN_WIDTH = 720;

/**
 * UC52 (KI-03): tổ trưởng / tổ phó may đặt công nhân cho từng công đoạn của mã hàng được chia cho tổ (DEV-ALL-49) trước khi nhận BTP (DEV-ALL-48). Mỗi công đoạn một
 * dòng, chọn công nhân ngay trên dòng; đơn nhiều công đoạn thì lọc, tích chọn nhiều dòng (giữ khi chuyển trang) rồi giao một lần.
 */
export function OperationAssignmentsPage() {
  const [copyOpen, setCopyOpen] = useState(false);
  const board = useAssignmentBoard();
  const { orderId, view, rows, products, current, workers, picked } = board;

  return (
    <PageCard title={text.title} description={text.subtitle}>
      <Space wrap>
        <OrderSelect
          value={orderId ?? undefined}
          onChange={board.changeOrder}
          placeholder={PRODUCTION_TEXT.selectOrder}
          notFoundContent={text.noOrders}
        />
        {products.length > 1 && (
          <Select<number>
            style={{ width: 320 }}
            aria-label={text.selectProduct}
            value={current?.productId}
            onChange={board.changeProduct}
            options={products.map((p) => ({
              value: p.productId,
              label:
                p.total === 0
                  ? fill(text.productNoOperations, p.productCode)
                  : fill(text.productOption, p.productCode, p.covered, p.total),
            }))}
          />
        )}
        <Button
          icon={<CopyOutlined />}
          disabled={orderId === null}
          onClick={() => setCopyOpen(true)}
        >
          {text.copy}
        </Button>
      </Space>

      <QueryError error={board.operations.error} onRetry={() => void board.operations.refetch()} />
      <QueryError
        error={board.assignments.error}
        onRetry={() => void board.assignments.refetch()}
      />
      <QueryError error={board.lines.error} onRetry={() => void board.lines.refetch()} />
      {board.noOperations.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={fill(text.noOperations, board.noOperations.join(', '))}
        />
      )}

      {orderId === null ? (
        <Empty description={text.selectOrderHint} style={{ margin: '48px 0' }} />
      ) : (
        <>
          {rows.length > 0 && (
            <>
              <AssignmentProgress total={rows.length} missing={board.missing} />
              <Segmented<AssignmentView>
                value={view}
                onChange={board.changeView}
                options={[
                  { value: 'operation', label: text.views.operation },
                  { value: 'worker', label: text.views.worker },
                ]}
              />
              {view === 'operation' && (
                <AssignmentFilterBar
                  rows={rows}
                  filter={board.filter}
                  workerOptions={workers.options}
                  onChange={board.changeFilter}
                />
              )}
            </>
          )}
          {view === 'worker' && rows.length > 0 && (
            <AssignmentByWorker orderId={orderId} rows={rows} workerOptions={workers.options} />
          )}
          {/* Chung một khối để thanh giao việc dính được trong suốt chiều dài bảng. */}
          <div hidden={view === 'worker' && rows.length > 0}>
            <DataTable<AssignmentRow>
              rowKey={(r) => r.operation.operationId}
              columns={assignmentColumns(orderId, workers)}
              dataSource={board.visible}
              loading={board.loading}
              locale={{ emptyText: rows.length === 0 ? text.empty : text.noMatch }}
              pagination={board.pagination(board.visible.length)}
              sticky={{ offsetHeader: APP_HEADER_HEIGHT }}
              scroll={{ x: TABLE_MIN_WIDTH }}
              rowSelection={{
                selectedRowKeys: picked,
                onChange: (keys) => board.setSelected(keys as number[]),
              }}
            />
            {picked.length > 0 && (
              <AssignmentBulkBar
                orderId={orderId}
                operationIds={picked}
                workerOptions={workers.options}
                onDone={() => board.setSelected([])}
              />
            )}
          </div>
          <CopyModal open={copyOpen} toOrderId={orderId} onClose={() => setCopyOpen(false)} />
        </>
      )}
    </PageCard>
  );
}
