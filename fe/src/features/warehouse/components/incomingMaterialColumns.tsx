import { EyeOutlined, FileAddOutlined } from '@ant-design/icons';
import type { ReactNode } from 'react';
import { Space, Tag, Tooltip, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { formatDeliveryWindow } from '@/utils/expectedDelivery';
import { INCOMING_TEXT, MATERIAL_SOURCE, PO_GOODS_STATUS_LABEL } from '../constants';
import type {
  ClosedShortPurchase,
  IncomingCustomerMaterial,
  IncomingSupplierMaterial,
  ReceiptPreset,
} from '../types';
import type { OrderGroup } from '../utils/groupByOrder';
import type { PurchaseOrderGroup } from '../utils/groupByPurchaseOrder';
import { MaterialCodeTip } from './MaterialCodeTip';

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
    <MaterialCodeTip code={l.materialCode} name={l.materialName} color={l.materialColor} />
  )),
  lineColumn<T>(text.ordered, 'ordered', (l) => formatQty(l.orderedQty), 'right'),
  lineColumn<T>(text.received, 'received', (l) => formatQty(l.receivedQty), 'right'),
];

/** NVL đầu tiên của đơn kèm thẻ "+N khác" (trỏ vào thì liệt kê mã các NVL còn lại). */
const materialsCell = (
  lines: { materialCode: string; materialName: string; materialColor: string }[],
) => {
  const [first] = lines;
  const more = lines.length - 1;
  return (
    <>
      {first && (
        <MaterialCodeTip
          code={first.materialCode}
          name={first.materialName}
          color={first.materialColor}
        />
      )}
      {more > 0 && (
        <Tooltip
          title={lines
            .slice(1)
            .map((l) => l.materialCode)
            .join(', ')}
        >
          <Tag style={{ marginLeft: 6 }}>
            +{more} {text.moreMaterials}
          </Tag>
        </Tooltip>
      )}
    </>
  );
};

export const customerColumns = (
  onReceive: OnReceive,
  onView: (orderId: number) => void,
): TableColumnsType<OrderGroup<IncomingCustomerMaterial>> => [
  {
    title: text.order,
    dataIndex: 'orderCode',
    sorter: (a, b) => a.orderCode.localeCompare(b.orderCode),
  },
  { title: text.material, key: 'material', render: (_, r) => materialsCell(r.lines) },
  {
    title: text.deliveries,
    key: 'deliveries',
    render: (_, r) => deliveryCell(r.expectedDeliveryCount, r.receivedCount),
  },
  {
    title: text.expectedDeliveryDate,
    key: 'expectedDeliveryDate',
    render: (_, r) => formatDeliveryWindow(r.expectedDeliveryDate, r.expectedDeliveryDateTo),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, r) => (
      <Space size={0}>
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => onView(r.orderId)}
        />
        {receiveAction(() =>
          onReceive({
            source: MATERIAL_SOURCE.CUSTOMER,
            orderId: r.orderId,
            acceptedQty: 0,
            customerLines: r.lines
              .filter((l) => l.outstandingQty > 0)
              .map((l) => ({ orderMaterialId: l.orderMaterialId, acceptedQty: l.outstandingQty })),
          }),
        )}
      </Space>
    ),
  },
];

export const supplierColumns = (
  onReceive: OnReceive,
  onView: (purchaseOrderId: number) => void,
): TableColumnsType<PurchaseOrderGroup<IncomingSupplierMaterial>> => [
  { title: text.purchaseOrder, dataIndex: 'poCode' },
  {
    title: text.order,
    dataIndex: 'orderCode',
    render: (v: string | null) => v ?? text.stockPurchase,
  },
  { title: text.supplier, dataIndex: 'supplierName', render: (v: string | null) => v ?? '—' },
  {
    title: text.material,
    key: 'material',
    render: (_, r) => {
      const first = r.lines[0];
      const more = r.lines.length - 1;
      return (
        <>
          {first && (
            <MaterialCodeTip
              code={first.materialCode}
              name={first.materialName}
              color={first.materialColor}
            />
          )}
          {more > 0 && (
            <Tooltip
              title={r.lines
                .slice(1)
                .map((l) => l.materialCode)
                .join(', ')}
            >
              <Tag style={{ marginLeft: 6 }}>
                +{more} {text.moreMaterials}
              </Tag>
            </Tooltip>
          )}
        </>
      );
    },
  },
  {
    title: text.deliveries,
    key: 'deliveries',
    render: (_, r) => (
      <Space direction="vertical" size={0}>
        {deliveryCell(r.expectedDeliveryCount, r.receivedCount)}
        {r.expectedDeliveryDate && (
          <Typography.Text type="secondary">
            {formatDeliveryWindow(r.expectedDeliveryDate, r.expectedDeliveryDateTo)}
          </Typography.Text>
        )}
      </Space>
    ),
  },
  {
    title: text.goodsStatus,
    dataIndex: 'goodsStatus',
    render: (v: string) => <StatusTag status={v} labels={PO_GOODS_STATUS_LABEL} />,
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, r) => (
      <Space size={0}>
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => onView(r.purchaseOrderId)}
        />
        {receiveAction(() =>
          onReceive({
            source: MATERIAL_SOURCE.SUPPLIER,
            purchaseOrderId: r.purchaseOrderId,
            acceptedQty: 0,
            purchaseLines: r.lines.map((l) => ({
              purchaseOrderLineId: l.purchaseOrderLineId,
              acceptedQty: l.outstandingQty,
            })),
          }),
        )}
      </Space>
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
