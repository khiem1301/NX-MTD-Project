import { useState } from 'react';
import { Alert, Button, Checkbox, Input, Space } from 'antd';
import { PlusOutlined, SearchOutlined, UploadOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useProductLookup } from '@/hooks/useOrderLookups';
import { normalizeText } from '@/utils/text';
import { OperationFormModal } from '../components/OperationFormModal';
import { OperationImportModal } from '../components/OperationImportModal';
import { PieceRateFormModal } from '../components/PieceRateFormModal';
import { operationColumns } from '../components/operationColumns';
import { PieceRateHistory } from '../components/PieceRateHistory';
import { ProductSelect } from '../components/ProductSelect';
import { PRODUCTION_TEXT } from '../constants';
import { useOperations, useSubmitPieceRate, useToggleOperation } from '../hooks/useProduction';
import type { Operation } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DetailModal } from '@/components/common/DetailModal';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';

const text = PRODUCTION_TEXT.operations;

/**
 * UC25 / UC26 (KI-01) — Kế toán nhập công đoạn của mã hàng từ file Excel quy trình công nghệ rồi đặt đơn giá ngay trên
 * từng công đoạn (có ngày hiệu lực, gửi Admin duyệt). Mở rộng một dòng để xem lịch sử đơn giá của công đoạn đó.
 */
export function OperationsPage() {
  const [productId, setProductId] = useState<number | null>(null);
  const [showDiscontinued, setShowDiscontinued] = useState(false);
  const [editing, setEditing] = useState<Operation | null>(null);
  const [rating, setRating] = useState<Operation | null>(null);
  const [creating, setCreating] = useState(false);
  /** Công đoạn vừa thêm: mở luôn ô nhập đơn giá khi danh sách đã tải lại có nó. */
  const [pendingRateId, setPendingRateId] = useState<number | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [groupFilter, setGroupFilter] = useState<string>();
  const [keyword, setKeyword] = useState('');
  const operations = useOperations(productId, showDiscontinued);
  const products = useProductLookup();
  const toggle = useToggleOperation();
  const submit = useSubmitPieceRate();

  const ratingTarget =
    rating ??
    (pendingRateId === null
      ? null
      : (operations.data?.find((o) => o.operationId === pendingRateId) ?? null));
  const product = products.data?.items.find((p) => p.productId === productId);
  const productLabel = product ? `${product.productCode} - ${product.productName}` : '';
  const hasActiveFinal = (operations.data ?? []).some(
    (o) => o.isFinalStep && o.status === 'Active',
  );
  const showNoFinalHint =
    productId !== null &&
    operations.data !== undefined &&
    operations.data.length > 0 &&
    !hasActiveFinal;

  const groups = [
    ...new Set(
      (operations.data ?? []).map((o) => o.groupName).filter((g): g is string => g !== null),
    ),
  ].sort();
  const needle = normalizeText(keyword.trim());
  const visible = (operations.data ?? []).filter(
    (o) =>
      (groupFilter === undefined || o.groupName === groupFilter) &&
      (needle === '' ||
        normalizeText(`${o.operationCode} ${o.operationName} ${o.groupName ?? ''}`).includes(
          needle,
        )),
  );

  const [viewing, setViewing] = useState<Operation | null>(null);

  const columns = operationColumns({
    onView: setViewing,
    onEdit: setEditing,
    onSetRate: setRating,
    onSubmitRate: (pieceRateId) => submit.mutate(pieceRateId),
    onToggle: (row, discontinue) => toggle.mutate({ id: row.operationId, discontinue }),
  });

  return (
    <PageCard
      title={text.title}
      description={text.subtitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={productId === null}
          onClick={() => setCreating(true)}
        >
          {text.add}
        </Button>
      }
    >
      <Space wrap>
        <ProductSelect
          value={productId ?? undefined}
          onChange={(v) => setProductId(v ?? null)}
          placeholder={PRODUCTION_TEXT.selectProduct}
        />
        <Checkbox
          checked={showDiscontinued}
          onChange={(e) => setShowDiscontinued(e.target.checked)}
        >
          {text.showDiscontinued}
        </Checkbox>
        <FilterSelect<string>
          placeholder={text.filterGroup}
          value={groupFilter}
          onChange={setGroupFilter}
          options={groups.map((g) => ({ value: g, label: g }))}
          style={{ width: 200 }}
        />
        <Input
          allowClear
          prefix={<SearchOutlined />}
          placeholder={text.searchOperation}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          style={{ width: 240 }}
        />
        <Button
          icon={<UploadOutlined />}
          disabled={productId === null}
          onClick={() => setImportOpen(true)}
        >
          {text.importExcel}
        </Button>
      </Space>

      {showNoFinalHint && <Alert type="info" showIcon message={text.noFinalStep} />}

      <QueryError error={operations.error} onRetry={() => void operations.refetch()} />

      <DataTable<Operation>
        rowKey="operationId"
        size="small"
        columns={columns}
        dataSource={visible}
        loading={operations.isFetching}
        locale={{
          emptyText: productId === null ? PRODUCTION_TEXT.selectProduct : MESSAGES.common.empty,
        }}
        scroll={{ x: 'max-content' }}
        pagination={false}
      />

      <DetailModal
        open={viewing !== null}
        onClose={() => setViewing(null)}
        title={viewing ? `${viewing.operationCode} - ${viewing.operationName}` : undefined}
        size="md"
      >
        {viewing && <PieceRateHistory operationId={viewing.operationId} />}
      </DetailModal>

      {productId !== null && (
        <>
          <OperationFormModal
            open={editing !== null || creating}
            productId={productId}
            operation={editing}
            groups={groups}
            onClose={() => {
              setEditing(null);
              setCreating(false);
            }}
            onCreated={setPendingRateId}
          />
          <PieceRateFormModal
            operation={ratingTarget}
            onClose={() => {
              setRating(null);
              setPendingRateId(null);
            }}
          />
          {importOpen && (
            <OperationImportModal
              open
              productId={productId}
              productLabel={productLabel}
              onClose={() => setImportOpen(false)}
            />
          )}
        </>
      )}
    </PageCard>
  );
}
