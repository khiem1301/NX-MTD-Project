import { Form, Select } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useOrderLineOptions } from '@/hooks/useLookups';
import { useBalances } from '@/hooks/useStockBalances';
import { LABELS, WAREHOUSE_TYPE } from '../constants';
import { ISSUE_LIMITS, PIECE_ISSUE_RULE } from '../utils/issueQuantity';
import { parsePieceKey, pieceKey } from '../utils/pieceKey';
import { IssueLinesSection } from './IssueLinesSection';
import { issueLineColumns, LINE_ITEM, LINES_SCROLL } from './issueLineColumns';
import { PieceSuggestionBar } from './PieceSuggestionBar';

type PieceIssueLinesProps = {
  orderId: number | undefined;
  /** Số lượng đơn xin theo dòng đơn; có thì là trần số cấp của dòng. */
  requested: Map<number, number>;
};

type LineValue = { pieceKey?: string; issuedQty?: number };

/**
 * Bảng dòng cấp BTP cho tổ may; cột Khả dụng lấy từ kho BTP. Mỗi lựa chọn là một dòng đơn (cả bộ) hoặc một chi tiết lẻ
 * đang có trong kho (hàng cắt bù); lựa chọn lưu ở field pieceKey = "dòng đơn:chi tiết".
 */
export function PieceIssueLines({ orderId, requested }: PieceIssueLinesProps) {
  const form = Form.useFormInstance();
  const current = Form.useWatch('lines', form) as LineValue[] | undefined;
  const lines = useOrderLineOptions(orderId);
  const balances = useBalances({
    warehouseType: WAREHOUSE_TYPE.SEMI_FINISHED,
    orderId,
    page: 1,
    pageSize: MAX_PAGE_SIZE,
  });
  const items = balances.data?.items ?? [];
  const available = new Map<number, number>(
    items.flatMap((b) =>
      b.productComponentMaterialId === null && b.orderLineId !== null
        ? [[b.orderLineId, b.availableQty] as [number, number]]
        : [],
    ),
  );
  const balanceByKey = new Map(
    items.map((b) => [pieceKey(b.orderLineId ?? 0, b.productComponentMaterialId), b.availableQty]),
  );
  const all = [
    ...(lines.data ?? []).map((l) => ({
      value: pieceKey(l.orderLineId, null),
      label: `${l.productCode} ${l.color}-${l.sizeCode}`,
    })),
    ...items
      .filter((b) => b.productComponentMaterialId !== null && b.availableQty > 0)
      .map((b) => ({
        value: pieceKey(b.orderLineId ?? 0, b.productComponentMaterialId),
        label: `${b.productCode} ${b.color}-${b.sizeCode} · ${b.componentName}`,
      })),
  ];
  // Theo đơn xin: chỉ các dòng đơn có trong đơn xin (cả bộ hoặc chi tiết lẻ của dòng đó), BE kiểm lại.
  const byRequest = requested.size > 0;
  const options = byRequest
    ? all.filter((o) => requested.has(parsePieceKey(o.value).orderLineId))
    : all;
  const taken = new Set((current ?? []).flatMap((l) => (l.pieceKey ? [l.pieceKey] : [])));
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <IssueLinesSection
          total={fields.length}
          filled={(current ?? []).filter((l) => (l.issuedQty ?? 0) > 0).length}
          addLabel={LABELS.addIssuePiece}
          addDisabled={
            orderId === undefined || options.length === 0 || fields.length >= ISSUE_LIMITS.maxLines
          }
          onAdd={() => add({})}
          suggestion={
            orderId !== undefined &&
            !byRequest && (
              <PieceSuggestionBar
                lines={lines.data ?? []}
                available={available}
                taken={taken}
                onAdd={(suggestions) => suggestions.forEach((s) => add(s))}
              />
            )
          }
        >
          <DataTable
            rowKey="key"
            size="small"
            pagination={false}
            scroll={LINES_SCROLL}
            dataSource={fields}
            locale={{ emptyText: LABELS.issueLinesEmpty }}
            columns={issueLineColumns({
              itemTitle: LABELS.piece,
              showRequested: requested.size > 0,
              onRemove: remove,
              lineOf: (index) => {
                const key = current?.[index]?.pieceKey;
                return {
                  rule: PIECE_ISSUE_RULE,
                  available: key ? (balanceByKey.get(key) ?? 0) : undefined,
                  requested: key ? requested.get(parsePieceKey(key).orderLineId) : undefined,
                };
              },
              renderItem: (field) => (
                <Form.Item name={[field.name, 'pieceKey']} rules={required} style={LINE_ITEM}>
                  <Select
                    showSearch
                    optionFilterProp="label"
                    placeholder={LABELS.orderLine}
                    options={options}
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              ),
              renderQty: (field, rule, max, _unit, limitRules) => (
                <Form.Item
                  name={[field.name, 'issuedQty']}
                  rules={[...required, ...limitRules]}
                  dependencies={[['lines', field.name, 'pieceKey']]}
                  style={LINE_ITEM}
                >
                  <QtyInput min={rule.min} max={max} style={{ width: '100%' }} />
                </Form.Item>
              ),
            })}
          />
        </IssueLinesSection>
      )}
    </Form.List>
  );
}
