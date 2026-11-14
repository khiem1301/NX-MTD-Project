import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Col, Form, Row, Select, Typography, type FormInstance } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import type { WorkOrderMaterialAllowance } from '@/types/workOrder';
import { formatQty } from '@/utils/format';
import { maxQtyRule } from '@/utils/qtyLimit';
import { REQUEST_LINE_LIMITS } from '../constants';
import { EMPTY_LINE } from '../hooks/useRequestForm';
import { useRequisitionItems } from '../hooks/useRequisitionItems';
import { lineQuantityRule, roundForMaterial } from '../utils/lineQuantity';
import { lineCap } from '../utils/requisitionItems';
import { remainingOf } from '../utils/workOrderRequisition';

type RequisitionLineFieldsProps = {
  form: FormInstance;
  requestType: 'MaterialRequisition' | 'SemiFinishedRequisition';
  orderId: number | undefined;
  /** Tổ may xin NVL và mọi đơn Xin BTP: phải chọn đơn hàng trước khi chọn hàng (DEV-ALL-110). */
  needsOrder: boolean;
  /** Đơn xin NVL theo lệnh cắt: chỉ chọn vải hệ thống tính được cho lệnh, không vượt số còn xin được (DEV-ALL-92). */
  allowance?: WorkOrderMaterialAllowance[];
};

type LineValue = { materialId?: number; orderLineId?: number; quantity?: number };

const ITEM_SPAN = { xs: 24, sm: 11 };
const STOCK_SPAN = { xs: 7, sm: 4 };
const QTY_SPAN = { xs: 10, sm: 5 };
const UNIT_SPAN = { xs: 4, sm: 3 };
const DELETE_SPAN = { xs: 3, sm: 1 };
const CELL_PAD = { paddingTop: 5 };

/**
 * Dòng của đơn Xin NVL / Xin BTP dạng bảng: chọn NVL (Xin NVL) hoặc mã hàng – màu – size của đơn (Xin BTP), cột Khả dụng cho biết
 * số trong kho; số xin vượt khả dụng (hoặc số còn xin được của lệnh cắt) thì báo lỗi dưới ô và không lưu được (DEV-ALL-110).
 */
export function RequisitionLineFields({
  form,
  requestType,
  orderId,
  needsOrder,
  allowance,
}: RequisitionLineFieldsProps) {
  const isPiece = requestType === 'SemiFinishedRequisition';
  const field = isPiece ? 'orderLineId' : 'materialId';
  // Đơn theo lệnh cắt cũng có orderId (lấy từ lệnh) nhưng chọn vải theo lệnh, không giới hạn theo NVL của đơn.
  const { items, isLoading, ready } = useRequisitionItems(
    requestType,
    needsOrder ? orderId : undefined,
    needsOrder,
  );
  const lines = Form.useWatch('lines', form) as LineValue[] | undefined;
  const allowed =
    allowance && new Set(allowance.filter((a) => a.capQty !== null).map((a) => a.materialId));
  const choosable = items.filter((i) => !allowed || allowed.has(i.id));
  const byId = new Map(items.map((i) => [i.id, i]));
  const itemOf = (index: number) => byId.get(lines?.[index]?.[field] ?? 0);
  const capOf = (index: number) =>
    lineCap(
      itemOf(index),
      isPiece ? undefined : remainingOf(allowance, lines?.[index]?.materialId),
    );

  /** Đổi NVL sang loại không cho số lẻ thì làm tròn số đã nhập để khỏi báo lỗi ở BE. */
  const handleItemChange = (index: number, id: number) => {
    const current = form.getFieldValue(['lines', index, 'quantity']) as number | undefined;
    if (current !== undefined && !isPiece)
      form.setFieldValue(
        ['lines', index, 'quantity'],
        roundForMaterial(current, byId.get(id)?.materialType ?? undefined),
      );
  };

  const itemLabel = isPiece ? MESSAGES.requests.pieceItem : MESSAGES.requests.materialPlaceholder;
  const placeholder = !ready
    ? MESSAGES.requests.orderFirst
    : isPiece
      ? MESSAGES.requests.piecePlaceholder
      : MESSAGES.requests.materialPlaceholder;
  const notFound = isPiece ? MESSAGES.requests.noOrderPieces : MESSAGES.requests.noOrderMaterials;

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          <Typography.Text type="secondary">{MESSAGES.requests.requisitionHint}</Typography.Text>
          <Row gutter={8} style={{ margin: '4px 0' }}>
            <Col {...ITEM_SPAN}>
              <Typography.Text strong>{itemLabel}</Typography.Text>
            </Col>
            <Col {...STOCK_SPAN}>
              <Typography.Text strong>{MESSAGES.requests.available}</Typography.Text>
            </Col>
            <Col {...QTY_SPAN}>
              <Typography.Text strong>{MESSAGES.requests.quantity}</Typography.Text>
            </Col>
            {!isPiece && (
              <Col {...UNIT_SPAN}>
                <Typography.Text strong>{MESSAGES.requests.unit}</Typography.Text>
              </Col>
            )}
          </Row>
          {fields.map((f) => {
            const item = itemOf(f.name);
            const cap = capOf(f.name);
            return (
              <Row key={f.key} gutter={8} align="top">
                <Col {...ITEM_SPAN}>
                  <Form.Item
                    name={[f.name, field]}
                    rules={[{ required: true, message: MESSAGES.common.required }]}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      disabled={!ready}
                      loading={isLoading}
                      placeholder={placeholder}
                      aria-label={itemLabel}
                      notFoundContent={needsOrder ? notFound : undefined}
                      onChange={(v: number) => handleItemChange(f.name, v)}
                      options={choosable.map((i) => ({ value: i.id, label: i.label }))}
                    />
                  </Form.Item>
                </Col>
                <Col {...STOCK_SPAN} style={CELL_PAD}>
                  {item ? formatQty(item.availableQty) : '—'}
                </Col>
                <Col {...QTY_SPAN}>
                  <Form.Item
                    name={[f.name, 'quantity']}
                    dependencies={[['lines', f.name, field]]}
                    rules={[
                      { required: true, message: MESSAGES.common.required },
                      maxQtyRule(cap?.cap, { message: cap?.message }),
                    ]}
                  >
                    <QtyInput
                      {...lineQuantityRule(requestType, item?.materialType ?? undefined)}
                      style={{ width: '100%' }}
                      aria-label={MESSAGES.requests.quantity}
                    />
                  </Form.Item>
                </Col>
                {!isPiece && (
                  <Col {...UNIT_SPAN} style={CELL_PAD}>
                    {item?.unit ?? '—'}
                  </Col>
                )}
                <Col {...DELETE_SPAN}>
                  {fields.length > 1 && (
                    <Button
                      type="text"
                      danger
                      icon={<DeleteOutlined />}
                      aria-label={MESSAGES.common.delete}
                      onClick={() => remove(f.name)}
                    />
                  )}
                </Col>
              </Row>
            );
          })}
          <Button
            type="dashed"
            block
            icon={<PlusOutlined />}
            disabled={fields.length >= REQUEST_LINE_LIMITS.maxLines}
            onClick={() => add(EMPTY_LINE)}
          >
            {MESSAGES.requests.addLine}
          </Button>
        </>
      )}
    </Form.List>
  );
}
