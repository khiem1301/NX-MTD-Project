import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Form, Select, Typography, type FormInstance } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { useOrderLineComponents } from '@/hooks/useLookups';
import type { OrderLineOption } from '@/types/lookup';
import { QtyInput } from '@/components/common/QtyInput';
import { LABELS } from '../constants';

const required = [{ required: true, message: MESSAGES.common.required }];
const NO_MARGIN = { marginBottom: 0 };
const MAX_PIECES = 1_000_000;
const COLUMN_WIDTH = { line: 180, pieces: 150, remove: 48 };

type ComponentSelectProps = { form: FormInstance; name: number };

/** Ô chọn thành phần: danh sách phụ thuộc màu – size đã chọn trên cùng dòng. */
function ComponentSelect({ form, name }: ComponentSelectProps) {
  const orderLineId = Form.useWatch(['extras', name, 'orderLineId'], form) as number | undefined;
  const components = useOrderLineComponents(orderLineId);
  return (
    <Form.Item name={[name, 'productComponentMaterialId']} rules={required} style={NO_MARGIN}>
      <Select
        size="small"
        placeholder={LABELS.component}
        disabled={orderLineId === undefined}
        loading={components.isLoading}
        options={(components.data ?? []).map((c) => ({
          value: c.productComponentMaterialId,
          label: c.componentName,
        }))}
      />
    </Form.Item>
  );
}

type ExtraComponentLinesProps = { form: FormInstance; lines: OrderLineOption[] };

/**
 * Lệnh cắt thường cắt lẻ thêm thành phần: bảng cùng kiểu bảng kế hoạch, mỗi dòng một màu – size, một thành phần và số chi tiết.
 * Chưa chọn mã hàng thì ẩn phần hiển thị nhưng vẫn giữ Form.List, để lệnh đang sửa không mất dòng cắt lẻ khi dòng đơn chưa tải xong.
 */
export function ExtraComponentLines({ form, lines }: ExtraComponentLinesProps) {
  return (
    <Form.List name="extras">
      {(fields, { add, remove }) =>
        lines.length === 0 ? null : (
          <div style={{ marginTop: 12 }}>
            <Flex justify="space-between" align="center" gap={8} wrap style={{ marginBottom: 8 }}>
              <div>
                <Typography.Text strong>{LABELS.extraComponents}</Typography.Text>{' '}
                <Typography.Text type="secondary">{LABELS.extraComponentsHint}</Typography.Text>
              </div>
              <Button size="small" icon={<PlusOutlined />} onClick={() => add()}>
                {LABELS.addExtraComponent}
              </Button>
            </Flex>
            {fields.length > 0 && (
              <DataTable
                rowKey="key"
                size="small"
                pagination={false}
                dataSource={fields}
                columns={[
                  {
                    title: LABELS.orderLine,
                    key: 'line',
                    width: COLUMN_WIDTH.line,
                    render: (_, field) => (
                      <Form.Item
                        name={[field.name, 'orderLineId']}
                        rules={required}
                        style={NO_MARGIN}
                      >
                        <Select
                          size="small"
                          placeholder={LABELS.orderLine}
                          options={lines.map((l) => ({
                            value: l.orderLineId,
                            label: `${l.color} - ${l.sizeCode}`,
                          }))}
                          onChange={() =>
                            form.setFieldValue(
                              ['extras', field.name, 'productComponentMaterialId'],
                              undefined,
                            )
                          }
                        />
                      </Form.Item>
                    ),
                  },
                  {
                    title: LABELS.component,
                    key: 'component',
                    render: (_, field) => <ComponentSelect form={form} name={field.name} />,
                  },
                  {
                    title: LABELS.extraPieces,
                    key: 'pieces',
                    width: COLUMN_WIDTH.pieces,
                    render: (_, field) => (
                      <Form.Item
                        name={[field.name, 'plannedQty']}
                        rules={required}
                        style={NO_MARGIN}
                      >
                        <QtyInput size="small" min={1} max={MAX_PIECES} style={{ width: '100%' }} />
                      </Form.Item>
                    ),
                  },
                  {
                    key: 'remove',
                    width: COLUMN_WIDTH.remove,
                    render: (_, field) => (
                      <IconAction
                        danger
                        title={MESSAGES.common.delete}
                        icon={<DeleteOutlined />}
                        onClick={() => remove(field.name)}
                      />
                    ),
                  },
                ]}
              />
            )}
          </div>
        )
      }
    </Form.List>
  );
}
