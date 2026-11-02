import { useMemo, useState } from 'react';
import { Alert, Button, Empty, Select, Space } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import {
  useAllottedOrderLines,
  useAssignableOperations,
  useAssignments,
} from '../hooks/useProduction';
import { useWorkerOptions } from '../hooks/useWorkerOptions';
import {
  ASSIGNMENT_STATE,
  assignmentProducts,
  buildAssignmentRows,
  filterAssignmentRows,
  type AssignmentFilter,
  type AssignmentRow,
} from '../utils/assignmentBoard';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AssignmentBulkBar } from '../components/AssignmentBulkBar';
import { AssignmentFilterBar } from '../components/AssignmentFilterBar';
import { AssignmentProgress } from '../components/AssignmentProgress';
import { assignmentColumns } from '../components/assignmentColumns';
import { CopyModal } from '../components/CopyModal';

const text = PRODUCTION_TEXT.assignments;
const NO_FILTER: AssignmentFilter = {
  keyword: undefined,
  group: undefined,
  workerId: undefined,
  state: ASSIGNMENT_STATE.ALL,
};
/** Chiều cao thanh đầu trang (layout.css), để đầu bảng dính ngay dưới nó khi cuộn. */
const APP_HEADER_HEIGHT = 64;
/** Bề rộng tối thiểu của bảng: màn hẹp hơn thì cuộn ngang, ô công nhân không bị cắt trên màn rộng. */
const TABLE_MIN_WIDTH = 720;

/**
 * UC52 (KI-03): tổ trưởng / tổ phó may đặt công nhân cho từng công đoạn của mã hàng được chia cho tổ (DEV-ALL-49) trước khi nhận BTP (DEV-ALL-48). Mỗi công đoạn một
 * dòng, chọn công nhân ngay trên dòng; đơn nhiều công đoạn thì lọc, tích chọn nhiều dòng (giữ khi chuyển trang) rồi giao một lần.
 */
export function OperationAssignmentsPage() {
  const [orderId, setOrderId] = useState<number | null>(null);
  const [productId, setProductId] = useState<number | null>(null);
  const [copyOpen, setCopyOpen] = useState(false);
  const { filters: filter, setFilters, pagination } = useTableQuery<AssignmentFilter>(NO_FILTER);
  const [selected, setSelected] = useState<number[]>([]);
  const assignments = useAssignments(orderId);
  const operations = useAssignableOperations(orderId);
  const lines = useAllottedOrderLines(orderId);
  const workers = useWorkerOptions();

  const allRows = useMemo(
    () =>
      buildAssignmentRows(
        operations.data ?? [],
        assignments.data ?? [],
        new Set(workers.options.map((o) => o.value)),
      ),
    [operations.data, assignments.data, workers.options],
  );
  // Điều kiện nhận BTP tính theo mã hàng trên phiếu nên mỗi lần chỉ làm một mã, tiến độ cũng theo mã đó.
  const products = useMemo(
    () => assignmentProducts(allRows, lines.data ?? []),
    [allRows, lines.data],
  );
  const current =
    products.find((p) => p.productId === productId) ??
    products.find((p) => p.total > 0) ??
    products[0];
  // Chỉ kết luận khi đã tải xong công đoạn, nếu không mọi mã đều trông như chưa khai.
  const noOperations = operations.isSuccess
    ? products.filter((p) => p.total === 0).map((p) => p.productCode)
    : [];
  const rows = allRows.filter((r) => r.operation.productId === current?.productId);
  const missing = rows.filter((r) => !r.isCovered).length;
  const visible = filterAssignmentRows(rows, filter);
  // Dòng đã tích có thể biến khỏi bảng (vừa có người khi lọc dòng thiếu, công đoạn ngừng dùng): không giao các dòng đó.
  const visibleIds = new Set(visible.map((r) => r.operation.operationId));
  const picked = selected.filter((id) => visibleIds.has(id));
  const loading = operations.isLoading || assignments.isLoading;

  const changeOrder = (id: number | undefined) => {
    setOrderId(id ?? null);
    setProductId(null);
    setFilters(NO_FILTER);
    setSelected([]);
  };
  const changeProduct = (id: number) => {
    setProductId(id);
    setFilters(NO_FILTER);
    setSelected([]);
  };
  // Đổi bộ lọc thì bỏ chọn, để không giao nhầm công đoạn đang bị ẩn.
  const changeFilter = (change: Partial<AssignmentFilter>) => {
    setFilters(change);
    setSelected([]);
  };

  return (
    <PageCard title={text.title} description={text.subtitle}>
      <Space wrap>
        <OrderSelect
          value={orderId ?? undefined}
          onChange={changeOrder}
          placeholder={PRODUCTION_TEXT.selectOrder}
          notFoundContent={text.noOrders}
        />
        {products.length > 1 && (
          <Select<number>
            style={{ width: 320 }}
            aria-label={text.selectProduct}
            value={current?.productId}
            onChange={changeProduct}
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

      <QueryError error={operations.error} onRetry={() => void operations.refetch()} />
      <QueryError error={assignments.error} onRetry={() => void assignments.refetch()} />
      <QueryError error={lines.error} onRetry={() => void lines.refetch()} />
      {noOperations.length > 0 && (
        <Alert type="warning" showIcon message={fill(text.noOperations, noOperations.join(', '))} />
      )}

      {orderId === null ? (
        <Empty description={text.selectOrderHint} style={{ margin: '48px 0' }} />
      ) : (
        <>
          {rows.length > 0 && (
            <>
              <AssignmentProgress total={rows.length} missing={missing} />
              <AssignmentFilterBar
                rows={rows}
                filter={filter}
                workerOptions={workers.options}
                onChange={changeFilter}
              />
            </>
          )}
          {/* Chung một khối để thanh giao việc dính được trong suốt chiều dài bảng. */}
          <div>
            <DataTable<AssignmentRow>
              rowKey={(r) => r.operation.operationId}
              columns={assignmentColumns(orderId, workers)}
              dataSource={visible}
              loading={loading}
              locale={{ emptyText: rows.length === 0 ? text.empty : text.noMatch }}
              pagination={pagination(visible.length)}
              sticky={{ offsetHeader: APP_HEADER_HEIGHT }}
              scroll={{ x: TABLE_MIN_WIDTH }}
              rowSelection={{
                selectedRowKeys: picked,
                onChange: (keys) => setSelected(keys as number[]),
              }}
            />
            {picked.length > 0 && (
              <AssignmentBulkBar
                orderId={orderId}
                operationIds={picked}
                workerOptions={workers.options}
                onDone={() => setSelected([])}
              />
            )}
          </div>
          <CopyModal open={copyOpen} toOrderId={orderId} onClose={() => setCopyOpen(false)} />
        </>
      )}
    </PageCard>
  );
}
