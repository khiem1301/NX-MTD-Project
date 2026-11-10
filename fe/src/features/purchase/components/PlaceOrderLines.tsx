import { Form, InputNumber, Space, Typography } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MATERIAL_QTY_DECIMALS, MAX_MATERIAL_QTY } from '@/constants/quantity';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';

type PlaceOrderLinesProps = {
  /** Dòng của đơn mua, cùng thứ tự với Form.List 'lines'. */
  orderLines: PurchaseOrderDetail['lines'];
};

/** Số lượng và đơn giá từng NVL khi đặt hàng (Form.List 'lines' của PlaceOrderModal). */
export function PlaceOrderLines({ orderLines }: PlaceOrderLinesProps) {
  const text = PURCHASE_TEXT.place;
  return (
    <Form.List name="lines">
      {(fields) =>
        fields.map((field, index) => {
          const line = orderLines[index];
          return (
            <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
              <Form.Item name={[field.name, 'purchaseOrderLineId']} hidden>
                <InputNumber />
              </Form.Item>
              <Typography.Text style={{ display: 'inline-block', width: 230 }}>
                {line
                  ? `${line.materialName}${line.materialColor ? ` (${line.materialColor})` : ''}`
                  : ''}
              </Typography.Text>
              <Form.Item
                name={[field.name, 'orderedQty']}
                rules={[{ required: true, type: 'number', min: 0.001, message: text.qtyRequired }]}
              >
                <QtyInput
                  decimals={MATERIAL_QTY_DECIMALS}
                  max={MAX_MATERIAL_QTY}
                  min={0}
                  addonAfter={line?.unit}
                  style={{ width: 170 }}
                />
              </Form.Item>
              <Form.Item
                name={[field.name, 'unitPrice']}
                rules={[{ required: true, type: 'number', min: 0.01, message: text.priceRequired }]}
              >
                <InputNumber
                  min={0}
                  precision={0}
                  addonAfter="₫"
                  placeholder={PURCHASE_TEXT.detail.unitPrice}
                  style={{ width: 170 }}
                />
              </Form.Item>
            </Space>
          );
        })
      }
    </Form.List>
  );
}
