import { Button, Form, InputNumber, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useOrderLineOptions } from '@/hooks/useLookups';
import { formatQty } from '@/utils/format';
import { LABELS, WAREHOUSE_TYPE } from '../constants';
import { useBalances } from '../hooks/useInventory';
import { pieceKey } from '../utils';

type PieceIssueLinesProps = { orderId: number | undefined };

/**
 * Dòng cấp BTP cho tổ may; hiện số khả dụng trong kho BTP. Mỗi lựa chọn là một dòng đơn (cả bộ) hoặc một chi tiết lẻ
 * đang có trong kho (hàng cắt bù); lựa chọn lưu ở field pieceKey = "dòng đơn:chi tiết".
 */
export function PieceIssueLines({ orderId }: PieceIssueLinesProps) {
  const lines = useOrderLineOptions(orderId);
  const balances = useBalances({
    warehouseType: WAREHOUSE_TYPE.SEMI_FINISHED,
    orderId,
    page: 1,
    pageSize: MAX_PAGE_SIZE,
  });
  const items = balances.data?.items ?? [];
  const available = new Map(
    items.filter((b) => b.productComponentId === null).map((b) => [b.orderLineId, b.availableQty]),
  );
  const wholeOptions = (lines.data ?? []).map((l) => ({
    value: pieceKey(l.orderLineId, null),
    label: `${l.productCode} ${l.color}-${l.sizeCode} (${LABELS.availableQty}: ${formatQty(available.get(l.orderLineId) ?? 0)})`,
  }));
  const partOptions = items
    .filter((b) => b.productComponentId !== null && b.availableQty > 0)
    .map((b) => ({
      value: pieceKey(b.orderLineId ?? 0, b.productComponentId),
      label: `${b.productCode} ${b.color}-${b.sizeCode} · ${b.componentName} (${LABELS.availableQty}: ${formatQty(b.availableQty)})`,
    }));
  const options = [...wholeOptions, ...partOptions];
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          {fields.map((field) => (
            <Space key={field.key} align="start" wrap>
              <Form.Item name={[field.name, 'pieceKey']} rules={required}>
                <Select placeholder={LABELS.orderLine} options={options} style={{ width: 380 }} />
              </Form.Item>
              <Form.Item name={[field.name, 'issuedQty']} rules={required}>
                <InputNumber
                  addonBefore={LABELS.issuedQty}
                  min={1}
                  precision={0}
                  style={{ width: 180 }}
                />
              </Form.Item>
              <Button icon={<DeleteOutlined />} onClick={() => remove(field.name)} />
            </Space>
          ))}
          <Button
            type="dashed"
            icon={<PlusOutlined />}
            onClick={() => add()}
            disabled={orderId === undefined}
          >
            {LABELS.addLine}
          </Button>
        </>
      )}
    </Form.List>
  );
}
