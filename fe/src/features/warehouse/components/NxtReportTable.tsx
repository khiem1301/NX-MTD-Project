import { type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatQty } from '@/utils/format';
import { LABELS, MATERIAL_TYPE_LABEL, WAREHOUSE_TYPE } from '../constants';
import { useNxtReport } from '../hooks/useInventory';
import type { NxtSummary } from '../types';

type NxtReportTableProps = {
  warehouseType: string;
  orderId: number;
  fromDate: string;
  toDate: string;
};

type Filters = { keyword?: string };

const qty = (title: string, dataIndex: keyof NxtSummary) => ({
  title,
  dataIndex,
  align: 'right' as const,
  render: formatQty,
});

const materialColumns: TableColumnsType<NxtSummary> = [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  {
    title: LABELS.materialType,
    dataIndex: 'materialType',
    render: (v: string | null) => (v ? MATERIAL_TYPE_LABEL[v] : '—'),
  },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string | null) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
];

const pieceColumns: TableColumnsType<NxtSummary> = [
  { title: LABELS.order, dataIndex: 'orderCode' },
  { title: LABELS.product, dataIndex: 'productCode' },
  { title: LABELS.garmentColor, dataIndex: 'color' },
  { title: LABELS.sizeCode, dataIndex: 'sizeCode' },
  {
    title: LABELS.component,
    dataIndex: 'componentName',
    render: (v: string | null) => v ?? LABELS.wholeGarment,
  },
];

/** Bảng NXT tổng hợp của một kho: tồn đầu / nhập / xuất / tồn cuối trong kỳ. */
export function NxtReportTable({ warehouseType, orderId, fromDate, toDate }: NxtReportTableProps) {
  const table = useTableQuery<Filters>({});
  const report = useNxtReport({
    ...table.params,
    warehouseType,
    orderId,
    fromDate,
    toDate,
  });
  const isMaterial = warehouseType === WAREHOUSE_TYPE.MATERIAL;

  const columns: TableColumnsType<NxtSummary> = [
    ...(isMaterial ? materialColumns : pieceColumns),
    qty(LABELS.openingQty, 'openingQty'),
    qty(LABELS.inQty, 'inQty'),
    qty(LABELS.outQty, 'outQty'),
    qty(LABELS.closingQty, 'closingQty'),
  ];

  return (
    <>
      <TableToolbar
        filters={
          <DebouncedSearch
            allowClear
            placeholder={MESSAGES.common.search}
            onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
            style={{ width: 260 }}
          />
        }
      />
      <QueryError error={report.error} onRetry={() => void report.refetch()} />
      <DataTable
        rowKey={(r) =>
          `${r.materialId ?? ''}|${r.orderLineId ?? ''}|${r.productComponentMaterialId ?? ''}`
        }
        columns={columns}
        dataSource={report.data?.items}
        loading={report.isFetching}
        pagination={table.pagination(report.data?.totalItems)}
      />
    </>
  );
}
