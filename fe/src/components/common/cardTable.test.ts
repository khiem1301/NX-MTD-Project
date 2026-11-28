import { withCardLabels } from './cardTable';

type Row = { qty: number };

describe('withCardLabels', () => {
  it('gắn tên cột vào ô, kể cả cột con, và giữ thuộc tính onCell sẵn có', () => {
    const [plain, group] =
      withCardLabels<Row>([
        { title: 'Số lượng', dataIndex: 'qty', onCell: () => ({ colSpan: 2 }) },
        { title: 'Nhóm', children: [{ title: 'Con', dataIndex: 'qty' }] },
      ]) ?? [];

    expect(plain?.onCell?.({ qty: 1 }, 0)).toEqual({ colSpan: 2, 'data-label': 'Số lượng' });
    const child = group && 'children' in group ? group.children[0] : undefined;
    expect(child?.onCell?.({ qty: 1 }, 0)).toEqual({ 'data-label': 'Con' });
  });

  it('tiêu đề không phải chữ thì ô không có nhãn', () => {
    const [column] = withCardLabels<Row>([{ title: () => null, dataIndex: 'qty' }]) ?? [];

    expect(column?.onCell?.({ qty: 1 }, 0)).toEqual({ 'data-label': undefined });
  });
});
