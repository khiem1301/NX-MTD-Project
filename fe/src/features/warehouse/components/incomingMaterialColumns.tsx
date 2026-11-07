import { FileAddOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';
import { Space, Tag, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { INCOMING_TEXT, MATERIAL_SOURCE, PO_GOODS_STATUS_LABEL } from '../constants';
import type {
  ClosedShortPurchase,
  IncomingCustomerMaterial,
  IncomingSupplierMaterial,
  ReceiptPreset,
} from '../types';
import type { PurchaseOrderGroup } from '../utils/groupByPurchaseOrder';

const text = INCOMING_TEXT;

type PurchaseLine = ClosedShortPurchase | IncomingSupplierMaterial;
type OnReceive = (preset: ReceiptPreset) => void;

/** "đã nhận / dự kiến" lần giao; đã nhận đủ số lần mà vẫn còn thiếu thì báo "Đủ lần giao" để Thủ kho biết hệ thống sẽ lập đơn thiếu NVL. */
const deliveryCell = (expected: number | null, received: number) => {
  if (expected === null)
    return <Typography.Text type="secondary">{text.noDeliveryCount}</Typography.Text>;
  return (
    <Space size={4}>
      {received} / {expected} {text.deliveryUnit}
      {received >= expected && <Tag color="orange">{text.deliveriesDone}</Tag>}
    </Space>
  );
};

const receiveAction = (onClick: () => void) => (
  <IconAction title={text.createReceipt} icon={<FileAddOutlined />} onClick={onClick} />
);

/** Một cột của đơn mua: các dòng NVL xếp chồng trong một ô, các cột số lượng thẳng hàng với cột NVL. */
const lineColumn = <T extends PurchaseLine>(
  title: string,
  key: string,
  cell: (line: T) => ReactNode,
  align?: 'right',
): TableColumnsType<PurchaseOrderGroup<T>>[number] => ({
  title,
  key,
  align,
  render: (_, r) => (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      {r.lines.map((l) => (
        <div key={l.purchaseOrderLineId}>{cell(l)}</div>
      ))}
    </Space>
  ),
});

/** Đơn mua, đơn hàng, nhà cung cấp, NVL, đặt, đã nhận: chung cho bảng chờ nhận và bảng đóng thiếu. */
const purchaseHeadColumns = <T extends PurchaseLine>(): TableColumnsType<PurchaseOrderGroup<T>> => [
  { title: text.purchaseOrder, dataIndex: 'poCode' },
  {
    title: text.order,
    dataIndex: 'orderCode',
    render: (v: string | null) => v ?? text.stockPurchase,
  },
  { title: text.supplier, dataIndex: 'supplierName', render: (v: string | null) => v ?? '—' },
  lineColumn<T>(text.material, 'material', (l) => (
    <>
      <b>{l.materialCode}</b> {l.materialName}
      {l.materialColor ? ` - ${l.materialColor}` : ''}
    </>
  )),
  lineColumn<T>(text.ordered, 'ordered', (l) => formatQty(l.orderedQty), 'right'),
  lineColumn<T>(text.received, 'received', (l) => formatQty(l.receivedQty), 'right'),
];

export const customerColumns = (
  onReceive: OnReceive,
): TableColumnsType<IncomingCustomerMaterial> => [
  { title: text.order, dataIndex: 'orderCode' },
  {
    title: text.material,
    key: 'material',
    render: (_, r) => (
      <>
        <b>{r.materialCode}</b> {r.materialName}
      </>
    ),
  },
  { title: text.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: text.customerAgreed, dataIndex: 'customerQty', align: 'right', render: formatQty },
  { title: text.received, dataIndex: 'receivedQty', align: 'right', render: formatQty },
  {
    title: text.outstanding,
    dataIndex: 'outstandingQty',
    align: 'right',
    render: (v: number, r) => (
      <b>
        {formatQty(v)} {r.unit}
      </b>
    ),
  },
  {
    title: text.deliveries,
    key: 'deliveries',
    render: (_, r) => deliveryCell(r.expectedDeliveryCount, r.receivedCount),
  },
  {
    title: text.expectedDeliveryDate,
    key: 'expectedDeliveryDate',
    render: (_, r) => formatDate(r.expectedDeliveryDate ?? null),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, r) =>
      receiveAction(() =>
        onReceive({
          source: MATERIAL_SOURCE.CUSTOMER,
          orderId: r.orderId,
          orderMaterialId: r.orderMaterialId,
          acceptedQty: r.outstandingQty,
        }),
      ),
  },
];

export const supplierColumns = (
  onReceive: OnReceive,
): TableColumnsType<PurchaseOrderGroup<IncomingSupplierMaterial>> => [
  ...purchaseHeadColumns<IncomingSupplierMaterial>(),
  lineColumn<IncomingSupplierMaterial>(
    text.outstanding,
    'outstanding',
    (l) => (
      <b>
        {formatQty(l.outstandingQty)} {l.unit}
      </b>
    ),
    'right',
  ),
  {
    title: text.deliveries,
    key: 'deliveries',
    render: (_, r) => deliveryCell(r.expectedDeliveryCount, r.receivedCount),
  },
  {
    title: text.expectedDeliveryDate,
    key: 'expectedDeliveryDate',
    render: (_, r) => formatDate(r.expectedDeliveryDate ?? null),
  },
  {
    title: text.goodsStatus,
    dataIndex: 'goodsStatus',
    render: (v: string) => (
      <Space direction="vertical" size={0}>
        <StatusTag status={v} labels={PO_GOODS_STATUS_LABEL} />
        <Typography.Text type="success">{text.canReceiveMore}</Typography.Text>
      </Space>
    ),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, r) =>
      receiveAction(() =>
        onReceive({
          source: MATERIAL_SOURCE.SUPPLIER,
          purchaseOrderId: r.purchaseOrderId,
          acceptedQty: 0,
          purchaseLines: r.lines.map((l) => ({
            purchaseOrderLineId: l.purchaseOrderLineId,
            acceptedQty: l.outstandingQty,
          })),
        }),
      ),
  },
];

export const closedShortColumns = (): TableColumnsType<PurchaseOrderGroup<ClosedShortPurchase>> => [
  ...purchaseHeadColumns<ClosedShortPurchase>(),
  lineColumn<ClosedShortPurchase>(
    text.notReplenished,
    'notReplenished',
    (l) => (
      <b>
        {formatQty(l.notReplenishedQty)} {l.unit}
      </b>
    ),
    'right',
  ),
  {
    title: text.goodsStatus,
    key: 'status',
    render: () => <StatusTag status="CLOSED_SHORT" labels={PO_GOODS_STATUS_LABEL} />,
  },
];
