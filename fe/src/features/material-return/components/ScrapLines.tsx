import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Col, Form, Row, Select, Typography } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { MAX_MATERIAL_QTY } from '@/constants/quantity';
import { useScrapMaterialOptions } from '@/hooks/useLookups';
import { RETURN_LABELS, qtyDecimalsRule } from '../constants';
import { decimalsOfUnit, type ScrapRow } from '../utils/returnLines';

const ITEM_SPAN = { xs: 24, sm: 12 };
const QTY_SPAN = { xs: 14, sm: 6 };
const UNIT_SPAN = { xs: 7, sm: 3 };
const DELETE_SPAN = { xs: 3, sm: 2 };
const CELL_PAD = { paddingTop: 5 };

/**
 * NVL phế phẩm trả thêm (DEV-ALL-112): chọn NVL phế phẩm trong danh mục (vải vụn theo kg, kim gãy theo cái…), nhập số lượng,
 * không có trần. Cả tổ cắt và tổ may dùng; đơn vị lấy từ NVL nên kg nhập được số lẻ, đơn vị khác là số nguyên.
 */
export function ScrapLines() {
  const scrap = useScrapMaterialOptions();
  const form = Form.useFormInstance();
  const rows = Form.useWatch('scrap', form) as ScrapRow[] | undefined;
  const byId = new Map((scrap.data ?? []).map((m) => [m.materialId, m]));

  return (
    <Form.List name="scrap">
      {(fields, { add, remove }) => (
        <>
          <Typography.Text strong>{RETURN_LABELS.scrapTitle}</Typography.Text>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            {RETURN_LABELS.scrapHint}
          </Typography.Paragraph>
          {fields.map((f) => {
            const material = byId.get(rows?.[f.name]?.materialId ?? 0);
            return (
              <Row key={f.key} gutter={8} align="top">
                <Col {...ITEM_SPAN}>
                  <Form.Item
                    name={[f.name, 'materialId']}
                    rules={[{ required: true, message: MESSAGES.common.required }]}
                  >
                    <Select
                      showSearch
                      optionFilterProp="label"
                      loading={scrap.isLoading}
                      placeholder={RETURN_LABELS.scrapPlaceholder}
                      aria-label={RETURN_LABELS.scrapPlaceholder}
                      options={(scrap.data ?? []).map((m) => ({
                        value: m.materialId,
                        label: `${m.materialCode} ${m.materialName}`,
                      }))}
                    />
                  </Form.Item>
                </Col>
                <Col {...QTY_SPAN}>
                  <Form.Item
                    name={[f.name, 'quantity']}
                    dependencies={[['scrap', f.name, 'materialId']]}
                    rules={[{ required: true, message: MESSAGES.common.required }, qtyDecimalsRule]}
                  >
                    <QtyInput
                      decimals={decimalsOfUnit(material?.unit ?? '')}
                      min={0}
                      max={MAX_MATERIAL_QTY}
                      style={{ width: '100%' }}
                      aria-label={RETURN_LABELS.returnQty}
                    />
                  </Form.Item>
                </Col>
                <Col {...UNIT_SPAN} style={CELL_PAD}>
                  {material?.unit ?? '—'}
                </Col>
                <Col {...DELETE_SPAN}>
                  <Button
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    aria-label={MESSAGES.common.delete}
                    onClick={() => remove(f.name)}
                  />
                </Col>
              </Row>
            );
          })}
          <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({})}>
            {RETURN_LABELS.scrapAdd}
          </Button>
        </>
      )}
    </Form.List>
  );
}
