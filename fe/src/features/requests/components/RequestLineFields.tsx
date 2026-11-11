import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Col, Form, Input, Row, Select, Typography, type FormInstance } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import type { RequestType } from '@/constants/requestTypes';
import { REQUEST_LINE_LIMITS } from '../constants';
import { EMPTY_LINE } from '../hooks/useRequestForm';
import { lineQuantityRule, roundForMaterial } from '../utils/lineQuantity';
import { remainingOf } from '../utils/workOrderRequisition';
import type { WorkOrderMaterialAllowance } from '@/types/workOrder';
import { fill, formatQty } from '@/utils/format';

type RequestLineFieldsProps = {
  form: FormInstance;
  requestType: RequestType | undefined;
  /** Đơn xin NVL theo lệnh cắt: chỉ chọn vải hệ thống tính được cho lệnh, không vượt số còn xin được (DEV-ALL-92). */
  allowance?: WorkOrderMaterialAllowance[];
};

type LineValue = { materialId?: number; quantity?: number };

const ITEM_SPAN = { xs: 24, sm: 14 };
const QTY_SPAN = { xs: 14, sm: 6 };
const UNIT_SPAN = { xs: 7, sm: 3 };
const DELETE_SPAN = { xs: 3, sm: 1 };

/**
 * Dòng của đơn xin, dạng bảng có tiêu đề cột. Xin NVL: chọn NVL trong danh mục, hiện đơn vị; vải nhập được số lẻ tối đa
 * 2 chữ số, NVL khác vải chỉ nhập số nguyên. Xin BTP: số nguyên (bộ). Loại khác (linh kiện, máy…): gõ tên hàng.
 */
export function RequestLineFields({ form, requestType, allowance }: RequestLineFieldsProps) {
  const materials = useMaterialOptions();
  const lines = Form.useWatch('lines', form) as LineValue[] | undefined;
  const isMaterial = requestType === 'MaterialRequisition';
  const byId = new Map((materials.data ?? []).map((m) => [m.materialId, m]));
  const allowed =
    allowance && new Set(allowance.filter((a) => a.capQty !== null).map((a) => a.materialId));
  const choosable = (materials.data ?? []).filter((m) => !allowed || allowed.has(m.materialId));
  const withinRemaining = (index: number) => ({
    validator: async (_rule: unknown, value?: number) => {
      const left = remainingOf(allowance, lines?.[index]?.materialId);
      if (left !== undefined && (value ?? 0) > left)
        throw new Error(fill(MESSAGES.requests.overRemaining, formatQty(left)));
    },
  });

  /** Đổi NVL sang loại không cho số lẻ thì làm tròn số đã nhập để khỏi báo lỗi ở BE. */
  const handleMaterialChange = (index: number, materialId: number) => {
    const current = form.getFieldValue(['lines', index, 'quantity']) as number | undefined;
    if (current !== undefined)
      form.setFieldValue(
        ['lines', index, 'quantity'],
        roundForMaterial(current, byId.get(materialId)?.materialType),
      );
  };

  const itemLabel = isMaterial
    ? MESSAGES.requests.materialPlaceholder
    : MESSAGES.requests.itemDescription;

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          <Row gutter={8} style={{ marginBottom: 4 }}>
            <Col {...ITEM_SPAN}>
              <Typography.Text strong>{itemLabel}</Typography.Text>
            </Col>
            <Col {...QTY_SPAN}>
              <Typography.Text strong>{MESSAGES.requests.quantity}</Typography.Text>
            </Col>
            {isMaterial && (
              <Col {...UNIT_SPAN}>
                <Typography.Text strong>{MESSAGES.requests.unit}</Typography.Text>
              </Col>
            )}
          </Row>
          {fields.map((f) => {
            const material = byId.get(lines?.[f.name]?.materialId ?? 0);
            return (
              <Row key={f.key} gutter={8} align="top">
                <Col {...ITEM_SPAN}>
                  {isMaterial ? (
                    <Form.Item
                      name={[f.name, 'materialId']}
                      rules={[{ required: true, message: MESSAGES.common.required }]}
                    >
                      <Select
                        showSearch
                        optionFilterProp="label"
                        loading={materials.isLoading}
                        placeholder={itemLabel}
                        aria-label={itemLabel}
                        onChange={(v: number) => handleMaterialChange(f.name, v)}
                        options={choosable.map((m) => ({
                          value: m.materialId,
                          label: `${m.materialCode} ${m.materialName}${m.color ? ` - ${m.color}` : ''}`,
                        }))}
                      />
                    </Form.Item>
                  ) : (
                    <Form.Item
                      name={[f.name, 'itemDescription']}
                      rules={[
                        { required: true, whitespace: true, message: MESSAGES.common.required },
                        { max: 300 },
                      ]}
                    >
                      <Input placeholder={itemLabel} aria-label={itemLabel} />
                    </Form.Item>
                  )}
                </Col>
                <Col {...QTY_SPAN}>
                  <Form.Item
                    name={[f.name, 'quantity']}
                    dependencies={[['lines', f.name, 'materialId']]}
                    rules={[
                      { required: true, message: MESSAGES.common.required },
                      withinRemaining(f.name),
                    ]}
                  >
                    <QtyInput
                      {...lineQuantityRule(requestType, material?.materialType)}
                      style={{ width: '100%' }}
                      aria-label={MESSAGES.requests.quantity}
                    />
                  </Form.Item>
                </Col>
                {isMaterial && (
                  <Col {...UNIT_SPAN} style={{ paddingTop: 5 }}>
                    {material?.unit ?? '—'}
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
