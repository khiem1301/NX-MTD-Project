import { useState } from 'react';
import { Alert, Button, DatePicker, InputNumber, Space, Table, Typography, type TableColumnsType } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { rampUp, sumRows, type DailyRow } from '../utils/dailyPlan';

type Props = {
  /** Do Form.Item truyền vào. */
  value?: DailyRow[];
  onChange?: (rows: DailyRow[]) => void;
  /** Kỳ KPI (YYYY-MM-DD); chưa chọn kỳ thì chưa nhập được ngày. */
  start: string | null;
  end: string | null;
};

const API_DATE = 'YYYY-MM-DD';
const text = KPI_TEXT.daily;

/**
 * Bảng chỉ tiêu từng ngày của KPI: mấy ngày đầu vào đơn tổ làm chậm hơn nên Quản đốc nhập riêng từng ngày.
 * "Chia tăng dần" gợi ý số cho cả kỳ (Quản đốc sửa tay sau); thêm được Chủ nhật tăng ca, xóa được ngày nghỉ.
 */
export function KpiDailyPlanTable({ value = [], onChange, start, end }: Props) {
  const [rampTotal, setRampTotal] = useState<number | null>(null);
  const rows = value;
  const emit = (next: DailyRow[]) => onChange?.([...next].sort((a, b) => a.workDate.localeCompare(b.workDate)));

  if (!start || !end) return <Alert type="info" showIcon message={text.pickPeriod} />;

  const setQty = (workDate: string, targetQty: number | null) =>
    emit(rows.map((r) => (r.workDate === workDate ? { ...r, targetQty } : r)));

  const applyRamp = () => {
    if (!rampTotal || rows.length === 0) return;
    const qty = rampUp(rampTotal, rows.length);
    emit(rows.map((r, i) => ({ ...r, targetQty: qty[i] ?? null })));
  };

  const columns: TableColumnsType<DailyRow> = [
    {
      title: text.date,
      dataIndex: 'workDate',
      render: (v: string) => {
        const d = dayjs(v);
        return `${d.format('DD/MM/YYYY')} (${text.weekdays[d.day()]})`;
      },
    },
    {
      title: text.qty,
      dataIndex: 'targetQty',
      render: (v: number | null, r) => (
        <InputNumber
          min={1}
          precision={0}
          value={v}
          status={v ? undefined : 'error'}
          onChange={(q) => setQty(r.workDate, q)}
          style={{ width: 120 }}
        />
      ),
    },
    {
      key: 'remove',
      width: 48,
      render: (_, r) => (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          aria-label={text.remove}
          onClick={() => emit(rows.filter((x) => x.workDate !== r.workDate))}
        />
      ),
    },
  ];

  const taken = new Set(rows.map((r) => r.workDate));

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <InputNumber
          min={1}
          precision={0}
          placeholder={text.rampTotal}
          value={rampTotal}
          onChange={setRampTotal}
          style={{ width: 180 }}
        />
        <Button onClick={applyRamp} disabled={!rampTotal || rows.length === 0}>
          {text.rampUp}
        </Button>
        <DatePicker
          placeholder={text.addDay}
          format="DD/MM/YYYY"
          value={null}
          disabledDate={(d) =>
            d.isBefore(dayjs(start), 'day') || d.isAfter(dayjs(end), 'day') || taken.has(d.format(API_DATE))
          }
          onChange={(d: Dayjs | null) => d && emit([...rows, { workDate: d.format(API_DATE), targetQty: null }])}
        />
      </Space>
      <Typography.Text type="secondary">{text.rampHint}</Typography.Text>
      <Table
        rowKey="workDate"
        size="small"
        columns={columns}
        dataSource={rows}
        pagination={false}
        scroll={{ y: 280 }}
        locale={{ emptyText: text.empty }}
      />
      <Typography.Text strong>
        {`${text.total}: ${formatQty(sumRows(rows))} · ${text.days}: ${rows.length}`}
      </Typography.Text>
    </Space>
  );
}
