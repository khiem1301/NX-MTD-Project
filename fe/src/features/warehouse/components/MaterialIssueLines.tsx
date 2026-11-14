import { useEffect } from 'react';
import { Form, Select, type FormInstance } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useMaterialOptions } from '@/hooks/useLookups';
import { useBalances } from '@/hooks/useStockBalances';
import { LABELS, WAREHOUSE_TYPE } from '../constants';
import { useWorkOrderFabrics } from '../hooks/useIssues';
import { useOrderMaterials } from '../hooks/useOrderMaterials';
import { allowedIssueItems } from '../utils/issueScope';
import { ISSUE_LIMITS, materialIssueRule, roundIssueQty } from '../utils/issueQuantity';
import { FabricSuggestionBar } from './FabricSuggestionBar';
import { IssueLinesSection } from './IssueLinesSection';
import { issueLineColumns, LINE_ITEM, LINES_SCROLL } from './issueLineColumns';

type MaterialIssueLinesProps = {
  form: FormInstance;
  workOrderId: number | undefined;
  /** Không theo lệnh cắt thì chỉ cấp NVL của đơn hàng này. */
  orderId: number | undefined;
  /** Chưa chọn đơn xin: chọn lệnh cắt thì liệt kê sẵn vải của lệnh (số cấp để trống cho Thủ kho nhập). */
  seedFromWorkOrder: boolean;
  /** Số lượng đơn xin theo NVL; có thì là trần số cấp của dòng. */
  requested: Map<number, number>;
};

const materialLabel = (code: string, name: string, color: string) =>
  `${code} ${name}${color ? ` - ${color}` : ''}`;

type LineValue = { materialId?: number; issuedQty?: number };

/**
 * Bảng dòng cấp NVL cho tổ cắt / tổ may. Chỉ chọn được hàng trong phạm vi phiếu: đơn xin, vải của lệnh cắt, hoặc NVL của đơn
 * hàng (chỉ, cúc… của đơn cũng cấp được). Số cấp theo loại NVL (vải lẻ 2 chữ số, NVL khác số nguyên); vượt khả dụng hoặc số trong
 * đơn xin thì cảnh báo nhưng vẫn lưu được.
 */
export function MaterialIssueLines({
  form,
  workOrderId,
  orderId,
  seedFromWorkOrder,
  requested,
}: MaterialIssueLinesProps) {
  const materials = useMaterialOptions();
  const fabrics = useWorkOrderFabrics(workOrderId);
  const orderMaterials = useOrderMaterials(workOrderId === undefined ? orderId : undefined);
  const balances = useBalances({
    warehouseType: WAREHOUSE_TYPE.MATERIAL,
    page: 1,
    pageSize: MAX_PAGE_SIZE,
  });
  const lines = Form.useWatch('lines', form) as LineValue[] | undefined;

  useEffect(() => {
    if (!seedFromWorkOrder || !fabrics.data) return;
    form.setFieldValue(
      'lines',
      fabrics.data.map((f) => ({ materialId: f.materialId, issuedQty: undefined })),
    );
  }, [seedFromWorkOrder, fabrics.data, form]);

  const byId = new Map((materials.data ?? []).map((m) => [m.materialId, m]));
  const planned = new Map((fabrics.data ?? []).map((f) => [f.materialId, f]));
  const stock = new Map((balances.data?.items ?? []).map((b) => [b.materialId, b.availableQty]));
  // Chỉ chọn hàng trong phạm vi phiếu: đơn xin → vải của lệnh cắt → NVL của đơn hàng (BE kiểm lại).
  const allowed = allowedIssueItems({
    requested: new Set(requested.keys()),
    workOrderItems: workOrderId === undefined ? undefined : fabrics.data?.map((f) => f.materialId),
    orderItems: orderId === undefined ? undefined : orderMaterials.data?.map((m) => m.materialId),
  });
  const labels = new Map<number, string>([
    ...(materials.data ?? []).map(
      (m) => [m.materialId, materialLabel(m.materialCode, m.materialName, m.color)] as const,
    ),
    ...(orderMaterials.data ?? []).map(
      (m) =>
        [m.materialId, materialLabel(m.materialCode, m.materialName, m.materialColor)] as const,
    ),
    ...(fabrics.data ?? []).map(
      (f) =>
        [f.materialId, materialLabel(f.materialCode, f.materialName, f.materialColor)] as const,
    ),
  ]);
  const options = [...allowed].map((id) => ({ value: id, label: labels.get(id) ?? String(id) }));

  const handleMaterialChange = (index: number, materialId: number) => {
    const qty = form.getFieldValue(['lines', index, 'issuedQty']) as number | undefined;
    if (qty !== undefined)
      form.setFieldValue(
        ['lines', index, 'issuedQty'],
        roundIssueQty(qty, byId.get(materialId)?.materialType),
      );
  };
  const notDuplicate = {
    validator: async (_rule: unknown, value?: number) => {
      if (value !== undefined && (lines ?? []).filter((l) => l.materialId === value).length > 1)
        throw new Error(MESSAGES.warehouse.duplicateMaterialLine);
    },
  };

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <IssueLinesSection
          total={fields.length}
          filled={(lines ?? []).filter((l) => (l.issuedQty ?? 0) > 0).length}
          addLabel={LABELS.addIssueMaterial}
          addDisabled={allowed.size === 0 || fields.length >= ISSUE_LIMITS.maxLines}
          onAdd={() => add({})}
          suggestion={
            workOrderId !== undefined && (
              <FabricSuggestionBar
                form={form}
                workOrderId={workOrderId}
                fabrics={fabrics.data ?? []}
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
              itemTitle: LABELS.issueMaterial,
              showRequested: requested.size > 0,
              onRemove: remove,
              lineOf: (index) => {
                const id = lines?.[index]?.materialId;
                const material = id === undefined ? undefined : byId.get(id);
                return {
                  rule: materialIssueRule(material?.materialType),
                  unit: material?.unit,
                  available:
                    id === undefined
                      ? undefined
                      : workOrderId === undefined
                        ? stock.get(id)
                        : planned.get(id)?.availableQty,
                  requested: id === undefined ? undefined : requested.get(id),
                };
              },
              renderItem: (field) => (
                <Form.Item
                  name={[field.name, 'materialId']}
                  rules={[{ required: true, message: MESSAGES.common.required }, notDuplicate]}
                  style={LINE_ITEM}
                >
                  <Select
                    showSearch
                    optionFilterProp="label"
                    placeholder={LABELS.issueMaterial}
                    loading={materials.isLoading}
                    options={options}
                    onChange={(v: number) => handleMaterialChange(field.name, v)}
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              ),
              renderQty: (field, rule, unit, limitRules) => (
                <Form.Item
                  name={[field.name, 'issuedQty']}
                  dependencies={[['lines', field.name, 'materialId']]}
                  rules={limitRules}
                  style={LINE_ITEM}
                >
                  <QtyInput
                    decimals={rule.decimals}
                    min={rule.min}
                    max={rule.max}
                    addonAfter={unit}
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              ),
            })}
          />
        </IssueLinesSection>
      )}
    </Form.List>
  );
}
