import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Form, Input, Select, Space, type FormInstance } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import type { RequestType } from '@/constants/requestTypes';
import { REQUEST_LINE_LIMITS } from '../constants';
import { EMPTY_LINE } from '../hooks/useRequestForm';
import { lineQuantityRule, roundForMaterial } from '../utils/lineQuantity';

type RequestLineFieldsProps = { form: FormInstance; requestType: RequestType | undefined };

type LineValue = { materialId?: number; quantity?: number };

/**
 * Dòng của đơn xin. Xin NVL: chọn NVL trong danh mục; vải nhập được số lẻ tối đa 2 chữ số, NVL khác vải chỉ nhập số nguyên.
 * Xin BTP: số nguyên (bộ). Loại khác (linh kiện, máy…): gõ tên hàng, số lẻ tối đa 3 chữ số.
 */
export function RequestLineFields({ form, requestType }: RequestLineFieldsProps) {
  const materials = useMaterialOptions();
  const lines = Form.useWatch('lines', form) as LineValue[] | undefined;
  const isMaterial = requestType === 'MaterialRequisition';
  const typeOf = new Map((materials.data ?? []).map((m) => [m.materialId, m.materialType]));

  /** Đổi NVL sang loại không cho số lẻ thì làm tròn số đã nhập để khỏi báo lỗi ở BE. */
  const handleMaterialChange = (index: number, materialId: number) => {
    const current = form.getFieldValue(['lines', index, 'quantity']) as number | undefined;
    if (current !== undefined)
      form.setFieldValue(
        ['lines', index, 'quantity'],
        roundForMaterial(current, typeOf.get(materialId)),
      );
  };

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          {fields.map((f) => (
            <Space key={f.key} align="baseline">
              {isMaterial ? (
                <Form.Item
                  name={[f.name, 'materialId']}
                  rules={[{ required: true, message: MESSAGES.common.required }]}
                >
                  <Select
                    showSearch
                    optionFilterProp="label"
                    loading={materials.isLoading}
                    placeholder={MESSAGES.requests.materialPlaceholder}
                    style={{ width: 340 }}
                    onChange={(v: number) => handleMaterialChange(f.name, v)}
                    options={(materials.data ?? []).map((m) => ({
                      value: m.materialId,
                      label: `${m.materialCode} ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
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
                  <Input placeholder={MESSAGES.requests.itemDescription} style={{ width: 340 }} />
                </Form.Item>
              )}
              <Form.Item
                name={[f.name, 'quantity']}
                rules={[{ required: true, message: MESSAGES.common.required }]}
              >
                <QtyInput
                  {...lineQuantityRule(requestType, typeOf.get(lines?.[f.name]?.materialId ?? 0))}
                  placeholder={MESSAGES.requests.quantity}
                />
              </Form.Item>
              {fields.length > 1 && (
                <MinusCircleOutlined
                  aria-label={MESSAGES.common.delete}
                  onClick={() => remove(f.name)}
                />
              )}
            </Space>
          ))}
          <Button
            type="dashed"
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
