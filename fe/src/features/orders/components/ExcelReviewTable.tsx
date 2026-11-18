import { Button, Space, Table, theme } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import type { RowState } from '../utils/excelReview';

const text = MESSAGES.orders.excelReview;
const TABLE_HEIGHT = 380;

type ExcelReviewTableProps<R extends { rowNumber: number }> = {
  rows: R[];
  columns: ColumnsType<R>;
  stateOf: (row: R) => RowState;
  onlyErrors: boolean;
  onAdd: () => void;
};

/** DEV-ALL-122: một bảng sửa được của popup kiểm tra Excel — dòng lỗi nền đỏ, dòng dùng NVL chưa có nền vàng, nút Thêm dòng. */
export function ExcelReviewTable<R extends { rowNumber: number }>({
  rows,
  columns,
  stateOf,
  onlyErrors,
  onAdd,
}: ExcelReviewTableProps<R>) {
  const { token } = theme.useToken();
  const background: Partial<Record<RowState, string>> = {
    error: token.colorErrorBg,
    newMaterial: token.colorWarningBg,
  };
  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Table<R>
        rowKey="rowNumber"
        size="small"
        pagination={false}
        columns={columns}
        dataSource={onlyErrors ? rows.filter((r) => stateOf(r) === 'error') : rows}
        locale={{ emptyText: text.noRows }}
        scroll={{ x: 'max-content', y: TABLE_HEIGHT }}
        onRow={(r) => {
          const color = background[stateOf(r)];
          return { style: color ? { background: color } : undefined };
        }}
      />
      <Button icon={<PlusOutlined />} size="small" onClick={onAdd}>
        {text.addRow}
      </Button>
    </Space>
  );
}
