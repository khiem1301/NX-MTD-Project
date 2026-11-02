import type { TablePaginationConfig } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import type { Operation, OperationGroup } from '../types';
import { OperationGroupBulkBar } from './OperationGroupBulkBar';

/** Chiều cao thanh đầu trang (layout.css), để đầu bảng dính ngay dưới nó khi cuộn. */
const APP_HEADER_HEIGHT = 64;
/**
 * Bề rộng tối thiểu của bảng: cột Tên công đoạn không đặt bề rộng nên giãn theo màn, tối thiểu khoảng 146px.
 * Màn hẹp hơn thì cuộn ngang, cột Thao tác vẫn ghim bên phải.
 */
const TABLE_MIN_WIDTH = 1070;

type OperationTableProps = {
  productId: number;
  columns: ColumnsType<Operation>;
  rows: Operation[];
  loading: boolean;
  pagination: TablePaginationConfig;
  selected: number[];
  onSelect: (ids: number[]) => void;
  groups: OperationGroup[];
  draftRateIds: number[];
};

/** Bảng công đoạn; đã tích dòng thì hiện thanh thao tác hàng loạt. Chung một khối để thanh dính được trong suốt chiều dài bảng. */
export function OperationTable({
  productId,
  columns,
  rows,
  loading,
  pagination,
  selected,
  onSelect,
  groups,
  draftRateIds,
}: OperationTableProps) {
  return (
    <div>
      <DataTable<Operation>
        rowKey="operationId"
        size="small"
        columns={columns}
        dataSource={rows}
        loading={loading}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: TABLE_MIN_WIDTH }}
        sticky={{ offsetHeader: APP_HEADER_HEIGHT }}
        pagination={pagination}
        rowSelection={{
          selectedRowKeys: selected,
          preserveSelectedRowKeys: true,
          onChange: (keys) => onSelect(keys as number[]),
        }}
      />
      {selected.length > 0 && (
        <OperationGroupBulkBar
          productId={productId}
          operationIds={selected}
          groups={groups}
          draftRateIds={draftRateIds}
          onDone={() => onSelect([])}
        />
      )}
    </div>
  );
}
