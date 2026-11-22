import { IconAction } from '@/components/common/IconAction';
import { ProfileOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Typography, type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatQty } from '@/utils/format';
import { LABELS, WAREHOUSE_TYPE } from '../constants';
import { useBalances } from '@/hooks/useStockBalances';
import type { MaterialOrderScope, StockBalance } from '@/types/stock';
import type { StockCardQuery } from '../types';
import { MaterialIdentityCell } from './MaterialIdentityCell';
import { StockCardModal } from './StockCardModal';
import { StockOrderFilter } from './StockOrderFilter';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type BalanceTableProps = { warehouseType: string };
type Filters = { keyword?: string; orderId?: number; orderScope?: MaterialOrderScope };

const qty = (title: string, dataIndex: keyof StockBalance) => ({
  title,
  dataIndex,
  align: 'right' as const,
  render: formatQty,
});

const materialColumns: TableColumnsType<StockBalance> = [
  {
    title: LABELS.material,
    key: 'material',
    render: (_, b) => (
      <MaterialIdentityCell
        code={b.materialCode ?? ''}
        name={b.materialName ?? ''}
        color={b.materialColor}
        unit={b.unit}
      />
    ),
  },
];

const pieceColumns: TableColumnsType<StockBalance> = [
  {
    title: LABELS.belongsTo,
    key: 'belongs',
    render: (_, b) => (
      <div>
        <div>{`${b.orderCode} · ${b.productCode}`}</div>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {[b.color, b.sizeCode, b.componentName ?? LABELS.wholeGarment].join(' · ')}
        </Typography.Text>
      </div>
    ),
  },
];

const describe = (b: StockBalance): string =>
  b.materialId !== null
    ? `${b.materialCode} ${b.materialName} (${b.materialColor || '—'})`
    : `${b.orderCode} ${b.productCode} ${b.color}-${b.sizeCode}${b.componentName ? ` (${b.componentName})` : ''}`;

/** Bảng tồn của một kho; mở thẻ kho ở cột thao tác. */
export function BalanceTable({ warehouseType }: BalanceTableProps) {
  const table = useTableQuery<Filters>({});
  const balances = useBalances({ ...table.params, warehouseType });
  const [card, setCard] = useState<{ query: StockCardQuery; title: string }>();
  const isMaterial = warehouseType === WAREHOUSE_TYPE.MATERIAL;

  const columns: TableColumnsType<StockBalance> = [
    ...(isMaterial ? materialColumns : pieceColumns),
    qty(LABELS.onHandQty, 'onHandQty'),
    qty(LABELS.reservedQty, 'reservedQty'),
    qty(LABELS.availableQty, 'availableQty'),
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, b) => (
        <IconAction
          title={LABELS.stockCard}
          icon={<ProfileOutlined />}
          onClick={() => openCard(b)}
        />
      ),
    },
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
            {isMaterial ? (
              <StockOrderFilter
                orderId={table.filters.orderId}
                orderScope={table.filters.orderScope}
                onChange={table.setFilters}
              />
            ) : (
              <OrderSelect
                allowClear
                placeholder={LABELS.orderFilter}
                value={table.filters.orderId}
                onChange={(orderId) => table.setFilters({ orderId })}
              />
            )}
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
