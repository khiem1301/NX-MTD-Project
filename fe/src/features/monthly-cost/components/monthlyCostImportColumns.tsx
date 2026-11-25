import { Button, Input, Select } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined } from '@ant-design/icons';
import { MoneyInput } from '@/components/common/MoneyInput';
import { QtyInput } from '@/components/common/QtyInput';
import { ReasonHover } from '@/components/common/ReasonHover';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import {
  MONTHLY_COST_CATEGORY_OPTIONS,
  MONTHLY_COST_ROW_STATUS_LABEL,
  MONTHLY_COST_TEXT,
  MONTHLY_COST_TYPE_OPTIONS,
} from '../constants';
import type { MonthlyCostDraftRow, MonthlyCostExcelPreview } from '../types';

const text = MONTHLY_COST_TEXT.previewColumns;
const NOTE_MAX = 300;
const MAX_QTY = 1_000_000;
const orNull = (v: string) => (v.trim() === '' ? null : v);

type Ctx = {
  /** Kết quả kiểm gần nhất của máy chủ (trạng thái + lỗi từng dòng, theo rowNumber). */
  data: MonthlyCostExcelPreview;
  update: (rowNumber: number, patch: Partial<MonthlyCostDraftRow>) => void;
  remove: (rowNumber: number) => void;
};

/** DEV-ALL-187: cột sửa được của bảng chi phí tháng trên popup nhập Excel; kết quả kiểm lấy từ máy chủ theo rowNumber. */
export const monthlyCostImportColumns = ({
  data,
  update,
  remove,
}: Ctx): ColumnsType<MonthlyCostDraftRow> => {
  const statusOf = (rowNumber: number) => data.rows.find((r) => r.rowNumber === rowNumber);
  const failed = (rowNumber: number) => statusOf(rowNumber)?.rowStatus === 'Error';
  return [
    { title: text.rowNumber, dataIndex: 'rowNumber', width: 64 },
    {
      title: text.costCategoryCode,
      key: 'category',
      width: 200,
      render: (_, r) => (
        <Select
          style={{ width: '100%' }}
          showSearch
          optionFilterProp="label"
          status={failed(r.rowNumber) && !r.costCategoryCode ? 'error' : undefined}
          options={MONTHLY_COST_CATEGORY_OPTIONS}
          value={r.costCategoryCode ?? undefined}
          onChange={(v: string) => update(r.rowNumber, { costCategoryCode: v })}
        />
      ),
    },
    {
      title: text.costType,
      key: 'type',
      width: 120,
      render: (_, r) => (
        <Select
          style={{ width: '100%' }}
          options={MONTHLY_COST_TYPE_OPTIONS}
          value={r.costType ?? undefined}
          onChange={(v: string) => update(r.rowNumber, { costType: v })}
        />
      ),
    },
    {
      title: text.productCode,
      key: 'product',
      width: 150,
      render: (_, r) => (
        <Input
          value={r.productCode ?? ''}
          maxLength={50}
          onChange={(e) => update(r.rowNumber, { productCode: orNull(e.target.value) })}
        />
      ),
    },
    {
      title: text.amount,
      key: 'amount',
      width: 190,
      render: (_, r) => (
        <MoneyInput
          style={{ width: '100%' }}
          value={r.amount}
          onChange={(v) => update(r.rowNumber, { amount: v })}
        />
      ),
    },
    {
      title: text.quantity,
      key: 'quantity',
      width: 120,
      render: (_, r) => (
        <QtyInput
          style={{ width: '100%' }}
          max={MAX_QTY}
          value={r.quantity}
          onChange={(v) => update(r.rowNumber, { quantity: v })}
        />
      ),
    },
    {
      title: text.note,
      key: 'note',
      width: 220,
      render: (_, r) => (
        <Input
          value={r.note ?? ''}
          maxLength={NOTE_MAX}
          onChange={(e) => update(r.rowNumber, { note: orNull(e.target.value) })}
        />
      ),
    },
    {
      title: text.status,
      key: 'status',
      width: 130,
      fixed: 'right',
      render: (_, r) => {
        const status = statusOf(r.rowNumber);
        if (!status) return null;
        return (
          <ReasonHover
            reasons={[status.errorMessage && { text: status.errorMessage, type: 'danger' }]}
          >
            <StatusTag status={status.rowStatus} labels={MONTHLY_COST_ROW_STATUS_LABEL} />
          </ReasonHover>
        );
      },
    },
    {
      key: 'remove',
      width: 48,
      fixed: 'right',
      render: (_, r) => (
        <Button
          type="text"
          danger
          size="small"
          aria-label={MESSAGES.common.delete}
          icon={<DeleteOutlined />}
          onClick={() => remove(r.rowNumber)}
        />
      ),
    },
  ];
};
