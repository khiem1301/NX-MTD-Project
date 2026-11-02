import { fireEvent, render, screen } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { DataTable } from './DataTable';

type Row = { id: number; name: string };
const rows = Array.from({ length: 25 }, (_, i) => ({ id: i + 1, name: `Dòng ${i + 1}` }));
const columns = [{ title: 'Tên', dataIndex: 'name' }];

const pickSize = async (label: string) => {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: MESSAGES.common.pageSizeLabel }));
  fireEvent.click(await screen.findByTitle(label));
};

describe('DataTable', () => {
  it('mặc định 10 dòng, đổi số dòng ở ô phía trên thì về trang 1 và hiện đủ', async () => {
    render(<DataTable<Row> rowKey="id" columns={columns} dataSource={rows} />);
    expect(screen.getByText('1–10 / 25 dòng')).toBeInTheDocument();

    await pickSize('50 / trang');

    expect(screen.getByText('1–25 / 25 dòng')).toBeInTheDocument();
    expect(screen.getByText('Dòng 25')).toBeInTheDocument();
  });

  it('bảng phân trang ở máy chủ: đổi số dòng thì báo về trang 1 với số dòng mới', async () => {
    const onChange = jest.fn();
    render(
      <DataTable<Row>
        rowKey="id"
        columns={columns}
        dataSource={rows.slice(0, 10)}
        pagination={{ current: 2, pageSize: 10, total: 25, onChange }}
      />,
    );

    await pickSize('20 / trang');

    expect(onChange).toHaveBeenCalledWith(1, 20);
  });

  it('bảng không phân trang thì không có ô chọn số dòng', () => {
    render(<DataTable<Row> rowKey="id" columns={columns} dataSource={rows} pagination={false} />);
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });
});
