import type { ReactNode } from 'react';
import { DeleteOutlined } from '@ant-design/icons';
import { Button, Typography, type FormListFieldData, type TableColumnsType } from 'antd';
import type { Rule } from 'antd/es/form';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import { effectiveMax, type IssueQtyRule } from '../utils/issueQuantity';

/** Ô nhập trong bảng không chừa khoảng dưới (lỗi vẫn hiện ngay dưới ô). */
export const LINE_ITEM = { marginBottom: 0 };

/** Bảng dòng cấp cuộn trong khung để Modal không dài ra khi phiếu nhiều dòng. */
export const LINES_SCROLL = { y: 360 };

export type IssueLineInfo = {
  rule: IssueQtyRule;
  unit?: string;
  /** Số khả dụng trong kho của hàng đã chọn. */
  available?: number;
  /** Số lượng trong đơn xin (nếu chọn đơn). */
  requested?: number;
};

type IssueLineColumnsOptions = {
  itemTitle: string;
  showRequested: boolean;
  lineOf: (index: number) => IssueLineInfo;
  onRemove: (index: number) => void;
  renderItem: (field: FormListFieldData) => ReactNode;
  renderQty: (
    field: FormListFieldData,
    rule: IssueQtyRule,
    max: number,
    unit: string | undefined,
    /** Kiểm số cấp không vượt khả dụng / đơn xin, báo lỗi ngay dưới ô (BE cũng chặn khi kho không đủ). */
    limitRules: Rule[],
  ) => ReactNode;
};

/** Số cấp vượt khả dụng hoặc vượt số trong đơn xin thì báo lỗi ở ô (thay vì để BE từ chối cả phiếu). */
const limitRule = (line: IssueLineInfo): Rule => ({
  validator: async (_rule, value?: number | null) => {
    if (value === undefined || value === null) return;
    if (line.available !== undefined && value > line.available)
      throw new Error(fill(LABELS.overAvailableQty, formatQty(line.available)));
    if (line.requested !== undefined && value > line.requested)
      throw new Error(fill(LABELS.overRequestQty, formatQty(line.requested)));
  },
});

const qtyText = (value: number | undefined) =>
  value === undefined ? <Typography.Text type="secondary">—</Typography.Text> : formatQty(value);

/**
 * Cột chung của bảng dòng cấp NVL / BTP: STT, hàng cấp, khả dụng, số trong đơn xin (khi có đơn), số cấp (trần = nhỏ nhất của
 * trần quy định, khả dụng và đơn xin) và nút xóa dòng.
 */
export const issueLineColumns = ({
  itemTitle,
  showRequested,
  lineOf,
  onRemove,
  renderItem,
  renderQty,
}: IssueLineColumnsOptions): TableColumnsType<FormListFieldData> => [
  { title: '#', key: 'no', width: 44, align: 'center', render: (_, __, i) => i + 1 },
  { title: itemTitle, key: 'item', render: (_, field) => renderItem(field) },
  {
    title: LABELS.availableQty,
    key: 'available',
    width: 110,
    align: 'right',
    render: (_, field) => qtyText(lineOf(field.name).available),
  },
  ...(showRequested
    ? [
        {
          title: LABELS.requestedQty,
          key: 'requested',
          width: 100,
          align: 'right' as const,
          render: (_: unknown, field: FormListFieldData) => qtyText(lineOf(field.name).requested),
        },
      ]
    : []),
  {
    title: LABELS.issuedQty,
    key: 'qty',
    width: 200,
    render: (_, field) => {
      const line = lineOf(field.name);
      return renderQty(
        field,
        line.rule,
        effectiveMax(line.rule, line.available, line.requested),
        line.unit,
        [limitRule(line)],
      );
    },
  },
  {
    // Không đặt key 'actions' để DataTable không ghim cột phải (cột ghim + cuộn dọc làm bảng thừa khoảng trống).
    title: '',
    key: 'remove',
    width: 44,
    align: 'center',
    render: (_, field) => (
      <Button
        type="text"
        danger
        size="small"
        icon={<DeleteOutlined />}
        aria-label={MESSAGES.common.delete}
        onClick={() => onRemove(field.name)}
      />
    ),
  },
];
