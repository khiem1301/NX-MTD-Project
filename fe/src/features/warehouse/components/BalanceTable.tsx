import { useState } from 'react';
import { type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatQty } from '@/utils/format';
import { LABELS, MATERIAL_TYPE_LABEL, WAREHOUSE_TYPE } from '../constants';
import { useBalances } from '../hooks/useInventory';
import type { StockBalance, StockCardQuery } from '../types';
import { StockCardModal } from './StockCardModal';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type BalanceTableProps = { warehouseType: string };
type Filters = { keyword?: string; orderId?: number };

const qty = (title: string, dataIndex: keyof StockBalance) => ({
  title,
  dataIndex,
  align: 'right' as const,
  render: formatQty,
});

const materialColumns: TableColumnsType<StockBalance> = [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  {
    title: LABELS.materialType,
    dataIndex: 'materialType',
    render: (v: string) => MATERIAL_TYPE_LABEL[v],
  },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
];

const pieceColumns: TableColumnsType<StockBalance> = [
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

const describe = (b: StockBalance): string =>
  b.materialId !== null
    ? `${b.materialCode} ${b.materialName} (${b.materialColor || '—'})`
    : `${b.orderCode} ${b.productCode} ${b.color}-${b.sizeCode}${b.componentName ? ` (${b.componentName})` : ''}`;

/** Bảng tồn của một kho; bấm mã ở cột đầu để mở thẻ kho. */
export function BalanceTable({ warehouseType }: BalanceTableProps) {
  const table = useTableQuery<Filters>({});
  const balances = useBalances({ ...table.params, warehouseType });
  const [card, setCard] = useState<{ query: StockCardQuery; title: string }>();
  const isMaterial = warehouseType === WAREHOUSE_TYPE.MATERIAL;

  const columns: TableColumnsType<StockBalance> = [
    ...(isMaterial ? materialColumns : pieceColumns).map((c, i) =>
      i === 0
        ? {
            ...c,
            render: (v: string, b: StockBalance) => (
              <DetailLink onClick={() => openCard(b)}>{v}</DetailLink>
            ),
          }
        : c,
    ),
    qty(LABELS.onHandQty, 'onHandQty'),
    qty(LABELS.reservedQty, 'reservedQty'),
    qty(LABELS.availableQty, 'availableQty'),
  ];

  const openCard = (b: StockBalance) =>
    setCard({
      title: describe(b),
      query: isMaterial
        ? {
            warehouseType,
            materialId: b.materialId ?? undefined,
          }
        : {
            warehouseType,
            orderLineId: b.orderLineId ?? undefined,
            productComponentMaterialId: b.productComponentMaterialId,
          },
    });

  return (
    <>
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 260 }}
            />
            <OrderSelect
              allowClear
              placeholder={LABELS.order}
              onChange={(orderId) => table.setFilters({ orderId })}
            />
          </>
        }
      />
      <QueryError error={balances.error} onRetry={() => void balances.refetch()} />
      <DataTable
        rowKey={(b) =>
          `${b.materialId ?? ''}|${b.orderLineId ?? ''}|${b.productComponentMaterialId ?? ''}`
        }
        columns={columns}
        dataSource={balances.data?.items}
        loading={balances.isFetching}
        pagination={table.pagination(balances.data?.totalItems)}
      />
      <StockCardModal
        query={card?.query}
        title={card?.title ?? ''}
        onClose={() => setCard(undefined)}
      />
    </>
  );
}
