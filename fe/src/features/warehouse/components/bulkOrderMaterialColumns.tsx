import { DeleteOutlined } from '@ant-design/icons';
import { Input, InputNumber, Select, Typography, type TableColumnsType } from 'antd';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { isExpectedDeliveryDateDisabled } from '@/utils/expectedDelivery';
import { toApiDate } from '@/utils/format';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import type { MaterialOption } from '@/types/lookup';
import { QtyInput } from '@/components/common/QtyInput';
import { MATERIAL_QTY_DECIMALS, MAX_MATERIAL_QTY } from '@/constants/quantity';
import { LABELS } from '../constants';
import { deliveryCountProblem, MAX_NOTE_LENGTH, qtyProblem } from '../utils/orderMaterialRules';
import { rowError, type BulkLine, type Draft, isChanged } from './bulkOrderMaterial';

type Options = {
  materialOptions: MaterialOption[];
  /** Mã NVL đã có trong bảng (dòng cũ + dòng mới) để ô chọn không cho chọn trùng. */
  usedMaterialIds: number[];
  /** NVL đã định sẵn (thêm theo mã hàng): dòng mới cũng không chọn lại NVL. */
  lockMaterial?: boolean;
  showErrors: boolean;
  onPatch: (key: string, change: Partial<Draft>) => void;
  onPickMaterial: (key: string, materialId: number) => void;
  onRemove: (key: string) => void;
};

const QTY_WIDTH = 130;
const DELIVERY_WIDTH = 90;
const DATE_WIDTH = 140;
const MATERIAL_WIDTH = 380;
const SMALL = { fontSize: 12 };

/** Cột bảng sửa cả đơn: dòng cũ chỉ đọc NVL, dòng mới chọn NVL; các ô số lượng, số lần giao, ghi chú sửa được. */
export const bulkOrderMaterialColumns = ({
  materialOptions,
  usedMaterialIds,
  lockMaterial = false,
  showErrors,
  onPatch,
  onPickMaterial,
  onRemove,
}: Options): TableColumnsType<BulkLine> => {
  const option = (line: BulkLine) => materialOptions.find((m) => m.materialId === line.materialId);
  const unitOf = (line: BulkLine) => line.original?.unit ?? option(line)?.unit;

  /** Tên, màu của NVL trên một dòng; bỏ qua phần không có. */
  const detailOf = (line: BulkLine) =>
    [
      line.original?.materialName ?? option(line)?.materialName,
      line.original?.materialColor ?? option(line)?.color,
    ]
      .filter(Boolean)
      .join(' · ');

  /** Cột số cần / số khách cấp: cùng ô nhập, cùng kiểm lỗi. */
  const qtyColumn = (
    field: 'requiredQty' | 'customerQty',
    title: string,
    name: string,
  ): TableColumnsType<BulkLine>[number] => ({
    title,
    key: field,
    width: QTY_WIDTH,
    render: (_, line) => (
      <QtyInput
        decimals={MATERIAL_QTY_DECIMALS}
        max={MAX_MATERIAL_QTY}
        size="small"
        style={{ width: '100%' }}
        suffix={unitOf(line)}
        status={showErrors && qtyProblem(name, line.draft[field]) ? 'error' : undefined}
        value={line.draft[field]}
        onChange={(v) => onPatch(line.key, { [field]: v ?? 0 })}
      />
    ),
  });

  return [
    {
      title: LABELS.material,
      key: 'material',
      width: MATERIAL_WIDTH,
      render: (_, line) => (
        <>
          {line.original || lockMaterial ? (
            <Typography.Text strong>
              {line.original?.materialCode ?? option(line)?.materialCode}{' '}
            </Typography.Text>
          ) : (
            <Select
              size="small"
              showSearch
              optionFilterProp="label"
              placeholder={LABELS.material}
              style={{ width: '100%' }}
              status={showErrors && line.materialId === null ? 'error' : undefined}
              value={line.materialId ?? undefined}
              options={materialOptions
                .filter(
                  (m) =>
                    m.materialId === line.materialId || !usedMaterialIds.includes(m.materialId),
                )
                .map((m) => ({
                  value: m.materialId,
                  label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''}`,
                }))}
              onChange={(id: number) => onPickMaterial(line.key, id)}
            />
          )}
          <Typography.Text type="secondary" style={SMALL}>
            {detailOf(line)}
          </Typography.Text>
        </>
      ),
    },
    qtyColumn('requiredQty', LABELS.requiredQty, LABELS.requiredQtyName),
    qtyColumn('customerQty', LABELS.customerQty, LABELS.customerQtyName),
    {
      title: LABELS.deliveryCountShort,
      key: 'expectedDeliveryCount',
      width: DELIVERY_WIDTH,
      render: (_, line) =>
        line.draft.customerQty > 0 ? (
          <InputNumber
            size="small"
            style={{ width: '100%' }}
            aria-label={LABELS.expectedDeliveryCount}
            status={
              showErrors &&
              (!line.draft.expectedDeliveryCount ||
                deliveryCountProblem(line.draft.expectedDeliveryCount))
                ? 'error'
                : undefined
            }
            value={line.draft.expectedDeliveryCount}
            onChange={(v) => onPatch(line.key, { expectedDeliveryCount: v })}
          />
        ) : (
          '—'
        ),
    },
    {
      title: LABELS.expectedDeliveryDate,
      key: 'expectedDeliveryDate',
      width: DATE_WIDTH,
      render: (_, line) =>
        line.draft.customerQty > 0 ? (
          <AppDatePicker
            size="small"
            style={{ width: '100%' }}
            aria-label={LABELS.expectedDeliveryDate}
            status={showErrors && !line.draft.expectedDeliveryDate ? 'error' : undefined}
            value={line.draft.expectedDeliveryDate ? dayjs(line.draft.expectedDeliveryDate) : null}
            disabledDate={(d) => isExpectedDeliveryDateDisabled(d)}
            onChange={(d) => onPatch(line.key, { expectedDeliveryDate: d ? toApiDate(d) : null })}
          />
        ) : (
          '—'
        ),
    },
    {
      title: LABELS.note,
      key: 'note',
      render: (_, line) => {
        const error = showErrors && isChanged(line) ? rowError(line) : null;
        return (
          <>
            <Input
              size="small"
              maxLength={MAX_NOTE_LENGTH}
              status={error ? 'error' : undefined}
              value={line.draft.note}
              onChange={(e) => onPatch(line.key, { note: e.target.value })}
            />
            {error && <Typography.Text type="danger">{error}</Typography.Text>}
          </>
        );
      },
    },
    {
      key: 'remove',
      width: 40,
      render: (_, line) =>
        line.original ? null : (
          <IconAction
            title={MESSAGES.common.delete}
            icon={<DeleteOutlined />}
            danger
            onClick={() => onRemove(line.key)}
          />
        ),
    },
  ];
};
