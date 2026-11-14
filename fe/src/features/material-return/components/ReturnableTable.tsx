import { Form } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { MAX_MATERIAL_QTY } from '@/constants/quantity';
import { formatQty } from '@/utils/format';
import { maxQtyRule } from '@/utils/qtyLimit';
import { RETURN_LABELS, qtyDecimalsRule } from '../constants';
import type { ReturnableLine } from '../types';
import { decimalsOfUnit, lineKey } from '../utils/returnLines';

type ReturnableTableProps = {
  lines: ReturnableLine[];
  /** Tiêu đề cột "đã dùng" (đã cắt / QC đạt); null = không có cột (tổ may trả NVL không theo đơn). */
  usedTitle: string | null;
};

const itemText = (l: ReturnableLine) =>
  `${l.itemCode} ${l.itemName}${l.itemColor && l.materialId !== null ? ` - ${l.itemColor}` : ''}`;

const qtyOrDash = (v: number | null) => (v === null ? '—' : formatQty(v));

/** Bảng hàng tổ còn trả được: số đã nhận, đã dùng, đã trả, còn trả được (trần) và ô nhập số trả lần này. */
export function ReturnableTable({ lines, usedTitle }: ReturnableTableProps) {
  return (
    <DataTable<ReturnableLine>
      rowKey={lineKey}
      size="small"
      pagination={false}
      dataSource={lines}
      columns={[
        { title: RETURN_LABELS.item, key: 'item', render: (_, l) => itemText(l) },
        { title: RETURN_LABELS.unit, dataIndex: 'unit' },
        {
          title: RETURN_LABELS.receivedQty,
          dataIndex: 'receivedQty',
          align: 'right',
          render: formatQty,
        },
        ...(usedTitle === null
          ? []
          : [
              {
                title: usedTitle,
                dataIndex: 'usedQty',
                align: 'right' as const,
                render: qtyOrDash,
              },
            ]),
        {
          title: RETURN_LABELS.returnedQty,
          dataIndex: 'returnedQty',
          align: 'right',
          render: formatQty,
        },
        {
          title: RETURN_LABELS.remainingQty,
          dataIndex: 'remainingQty',
          align: 'right',
          render: formatQty,
        },
        {
          title: RETURN_LABELS.returnQty,
          key: 'qty',
          render: (_, l) => (
            <Form.Item
              name={['qty', lineKey(l)]}
              rules={[qtyDecimalsRule, maxQtyRule(l.remainingQty)]}
              style={{ marginBottom: 0 }}
            >
              <QtyInput decimals={decimalsOfUnit(l.unit)} min={0} max={MAX_MATERIAL_QTY} />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
