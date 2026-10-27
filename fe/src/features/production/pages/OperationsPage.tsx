import { useState } from 'react';
import { Alert, Button, Checkbox, Input, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { SearchOutlined, UploadOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useProductLookup } from '@/hooks/useOrderLookups';
import { formatCurrency, formatDate, formatQty } from '@/utils/format';
import { normalizeText } from '@/utils/text';
import { OperationFormModal } from '../components/OperationFormModal';
import { OperationImportModal } from '../components/OperationImportModal';
import { PieceRateFormModal } from '../components/PieceRateFormModal';
import { PieceRateHistory } from '../components/PieceRateHistory';
import { ProductSelect } from '../components/ProductSelect';
import { PRODUCTION_TEXT } from '../constants';
import { useOperations, useSubmitPieceRate, useToggleOperation } from '../hooks/useProduction';
import type { Operation } from '../types';
import { editableRate } from '../utils';

const text = PRODUCTION_TEXT.operations;
const rateText = PRODUCTION_TEXT.pieceRates;

/** Đặt / sửa đơn giá được khi công đoạn còn dùng và chưa có bản nào đang chờ Admin duyệt. */
const canSetRate = (operation: Operation) =>
  operation.status === 'Active' && operation.nextRate?.status !== 'PendingApproval';

/**
 * UC25 / UC26 (KI-01) — Kế toán nhập công đoạn của mã hàng từ file Excel quy trình công nghệ rồi đặt đơn giá ngay trên
 * từng công đoạn (có ngày hiệu lực, gửi Admin duyệt). Mở rộng một dòng để xem lịch sử đơn giá của công đoạn đó.
 */
export function OperationsPage() {
  const [productId, setProductId] = useState<number | null>(null);
  const [showDiscontinued, setShowDiscontinued] = useState(false);
  const [editing, setEditing] = useState<Operation | null>(null);
  const [rating, setRating] = useState<Operation | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [groupFilter, setGroupFilter] = useState<string>();
  const [keyword, setKeyword] = useState('');
  const operations = useOperations(productId, showDiscontinued);
  const products = useProductLookup();
  const toggle = useToggleOperation();
  const submit = useSubmitPieceRate();

  const product = products.data?.items.find((p) => p.productId === productId);
  const productLabel = product ? `${product.productCode} - ${product.productName}` : '';
  const hasActiveFinal = (operations.data ?? []).some((o) => o.isFinalStep && o.status === 'Active');
  const showNoFinalHint = productId !== null && operations.data !== undefined && operations.data.length > 0 && !hasActiveFinal;

  const groups = [...new Set((operations.data ?? []).map((o) => o.groupName).filter((g): g is string => g !== null))].sort();
  const needle = normalizeText(keyword.trim());
  const visible = (operations.data ?? []).filter(
    (o) =>
      (groupFilter === undefined || o.groupName === groupFilter) &&
      (needle === '' || normalizeText(`${o.operationCode} ${o.operationName} ${o.groupName ?? ''}`).includes(needle)),
  );

  const columns: ColumnsType<Operation> = [
    { title: text.columns.sequence, dataIndex: 'stationSequence', width: 80 },
    { title: text.columns.code, dataIndex: 'operationCode' },
    { title: text.columns.name, dataIndex: 'operationName' },
    { title: text.columns.group, dataIndex: 'groupName', render: (v: string | null) => (v ? <Tag color="geekblue">{v}</Tag> : '—') },
    {
      title: text.columns.usesPieces,
      dataIndex: 'usesSemiFinished',
      render: (v: boolean) => (v ? <Tag color="blue">{text.withPieces}</Tag> : <Tag>{text.withoutPieces}</Tag>),
    },
    { title: text.columns.skill, dataIndex: 'skillLevel', render: (v: number | null) => v ?? '—' },
    {
      title: text.columns.standardTime,
      dataIndex: 'standardTimeSec',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatQty(v)),
    },
    { title: text.columns.isFinal, dataIndex: 'isFinalStep', render: (v: boolean) => (v ? <Tag color="blue">Cuối</Tag> : '—') },
    {
      title: text.columns.rate,
      dataIndex: 'currentRate',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    {
      title: text.columns.nextRate,
      key: 'nextRate',
      render: (_, row) =>
        row.nextRate ? (
          <Space direction="vertical" size={0}>
            <span>
              {formatCurrency(row.nextRate.rate)} · từ {formatDate(row.nextRate.effectiveDate)}
            </span>
            <StatusTag status={row.nextRate.status} labels={rateText.status} />
            {row.nextRate.rejectReason && <Typography.Text type="danger">{row.nextRate.rejectReason}</Typography.Text>}
          </Space>
        ) : (
          '—'
        ),
    },
    { title: text.columns.status, dataIndex: 'status', render: (v: string) => <StatusTag status={v} labels={text.status} /> },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Space size={0} wrap>
          <Button type="link" onClick={() => setEditing(row)}>
            {MESSAGES.common.edit}
          </Button>
          {canSetRate(row) && (
            <Button type="link" onClick={() => setRating(row)}>
              {editableRate(row) ? rateText.editRate : rateText.setRate}
            </Button>
          )}
          {row.nextRate?.status === 'Draft' && (
            <Popconfirm title={rateText.submitConfirm} onConfirm={() => submit.mutate(row.nextRate!.pieceRateId)}>
              <Button type="link">{rateText.submit}</Button>
            </Popconfirm>
          )}
          {row.status === 'Active' ? (
            <Popconfirm title={text.discontinueConfirm} onConfirm={() => toggle.mutate({ id: row.operationId, discontinue: true })}>
              <Button type="link" danger>
                {text.discontinue}
              </Button>
            </Popconfirm>
          ) : (
            <Button type="link" onClick={() => toggle.mutate({ id: row.operationId, discontinue: false })}>
              {text.reactivate}
            </Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {text.title}
        </Typography.Title>
        <Typography.Text type="secondary">{text.subtitle}</Typography.Text>
      </div>

      <Space wrap>
        <ProductSelect value={productId ?? undefined} onChange={(v) => setProductId(v ?? null)} placeholder={PRODUCTION_TEXT.selectProduct} />
        <Checkbox checked={showDiscontinued} onChange={(e) => setShowDiscontinued(e.target.checked)}>
          {text.showDiscontinued}
        </Checkbox>
        <Select
          allowClear
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
        <Button type="primary" icon={<UploadOutlined />} disabled={productId === null} onClick={() => setImportOpen(true)}>
          {text.importExcel}
        </Button>
      </Space>

      {showNoFinalHint && <Alert type="info" showIcon message={text.noFinalStep} />}

      <QueryError error={operations.error} onRetry={() => void operations.refetch()} />

      <Table<Operation>
        rowKey="operationId"
        size="middle"
        columns={columns}
        dataSource={visible}
        loading={operations.isFetching}
        locale={{ emptyText: productId === null ? PRODUCTION_TEXT.selectProduct : MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        pagination={false}
        expandable={{ expandedRowRender: (row) => <PieceRateHistory operationId={row.operationId} />, rowExpandable: () => true }}
      />

      {productId !== null && (
        <>
          <OperationFormModal
            open={editing !== null}
            productId={productId}
            operation={editing}
            groups={groups}
            onClose={() => setEditing(null)}
          />
          <PieceRateFormModal operation={rating} onClose={() => setRating(null)} />
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
    </Space>
  );
}
