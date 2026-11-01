import { Alert, type TableColumnsType } from 'antd';
import { formatDate, formatDateTime, formatQty } from '@/utils/format';
import { LABELS, STOCK_DOC_LABEL } from '../constants';
import { useStockCard } from '../hooks/useInventory';
import type { StockCardEntry, StockCardQuery } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';

type StockCardModalProps = {
  query: StockCardQuery | undefined;
  title: string;
  onClose: () => void;
};

const columns: TableColumnsType<StockCardEntry> = [
  { title: LABELS.transactionDate, dataIndex: 'transactionDate', render: formatDate },
  {
    title: LABELS.document,
    key: 'doc',
    render: (_, e) =>
      `${STOCK_DOC_LABEL[e.sourceDocType] ?? e.sourceDocType} ${e.sourceDocCode ?? `#${e.sourceDocId}`}`,
  },
  {
    title: LABELS.qtyChange,
    dataIndex: 'qtyChange',
    align: 'right',
    render: (v: number) => (v > 0 ? `+${formatQty(v)}` : formatQty(v)),
  },
  { title: LABELS.balanceAfter, dataIndex: 'balanceAfter', align: 'right', render: formatQty },
  { title: LABELS.submittedAt, dataIndex: 'createdAt', render: formatDateTime },
];

/** UC24: thẻ kho của một dòng tồn (chứng từ, ngày, số nhập / xuất, tồn sau). */
export function StockCardModal({ query, title, onClose }: StockCardModalProps) {
  const card = useStockCard(query);
  return (
    <DetailModal
      open={query !== undefined}
      onClose={onClose}
      title={`${LABELS.stockCard}: ${title}`}
      size="md"
    >
      {card.error && <Alert type="error" message={card.error.message} />}
      <DataTable
        rowKey="stockTransactionId"
        columns={columns}
        dataSource={card.data}
        loading={card.isFetching}
        pagination={false}
      />
    </DetailModal>
  );
}
