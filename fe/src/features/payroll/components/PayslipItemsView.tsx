import { Descriptions, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { PAYROLL_TEXT } from '../constants';
import type { PayslipItem } from '../types';
import { formatCurrency } from '../utils';

type Props = {
  workDays: number | null;
  totalAdditions: number;
  totalDeductions: number;
  insurance: number;
  netPay: number;
  items: PayslipItem[];
};

type Section = { title: string; items: PayslipItem[] };

/** Tiền "1.250.000 ₫", số công/giờ giữ nguyên, bậc thợ là chữ (theo `unit` BE trả về). */
const formatItemValue = (item: PayslipItem): string => {
  if (item.unit === 'Text') return item.textValue ?? '—';
  if (item.unit === 'Number') return String(item.amount);
  return formatCurrency(item.amount);
};

const itemColumns: ColumnsType<PayslipItem> = [
  { title: PAYROLL_TEXT.previewColumns.itemName, dataIndex: 'itemName' },
  {
    title: PAYROLL_TEXT.previewColumns.amount,
    key: 'value',
    align: 'right',
    render: (_, item) => formatItemValue(item),
  },
];

/** Gom khoản theo nhóm hiển thị; BE đã sắp sẵn theo thứ tự mẫu phiếu nên giữ thứ tự xuất hiện đầu tiên. */
const groupBySection = (items: PayslipItem[]): Section[] => {
  const sections: Section[] = [];
  for (const item of items) {
    const existing = sections.find((s) => s.title === item.section);
    if (existing) existing.items.push(item);
    else sections.push({ title: item.section, items: [item] });
  }
  return sections;
};

/**
 * Phiếu lương theo các nhóm của mẫu phiếu giấy đang dùng (thông tin chung, ngày công giờ công, lương sản phẩm,
 * phụ cấp, tăng giờ, thưởng, các khoản trừ...) + thực nhận (UC06/UC31/UC32).
 * Dùng chung cho Admin xem chi tiết từng phiếu lương trong bảng lương và công nhân xem phiếu lương của mình.
 */
export function PayslipItemsView({
  workDays,
  totalAdditions,
  totalDeductions,
  insurance,
  netPay,
  items,
}: Props) {
  return (
    <div>
      <Descriptions column={{ xs: 1, sm: 2 }} bordered size="small" style={{ marginBottom: 16 }}>
        <Descriptions.Item label={PAYROLL_TEXT.previewColumns.workDays}>
          {workDays ?? '—'}
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.table.totalAdditions}>
          {formatCurrency(totalAdditions)}
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.table.totalDeductions}>
          {formatCurrency(totalDeductions)}
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.table.insurance}>
          {formatCurrency(insurance)}
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.myPayslip.netPayTotal} span={2}>
          <Typography.Text strong style={{ fontSize: 16 }}>
            {formatCurrency(netPay)}
          </Typography.Text>
        </Descriptions.Item>
      </Descriptions>

      {items.length === 0 ? (
        <Typography.Text type="secondary">{MESSAGES.common.empty}</Typography.Text>
      ) : (
        groupBySection(items).map(({ title, items: sectionItems }) => (
          <div key={title} style={{ marginBottom: 16 }}>
            <Typography.Title level={5}>{title}</Typography.Title>
            <Table<PayslipItem>
              rowKey={(row) => `${row.sortOrder}-${row.itemName}`}
              size="small"
              columns={itemColumns}
              dataSource={sectionItems}
              pagination={false}
              showHeader={false}
            />
          </div>
        ))
      )}
    </div>
  );
}
