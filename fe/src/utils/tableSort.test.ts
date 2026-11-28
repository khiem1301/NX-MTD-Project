import { compareValues, toServerSort, withDefaultSorter, withServerSorter } from './tableSort';

type Row = { name: string; qty: number | null; info: { code: string } };

describe('compareValues', () => {
  it('số sắp theo giá trị, chữ có số sắp theo số, ô trống xếp trước', () => {
    expect(compareValues(9, 10)).toBeLessThan(0);
    expect(compareValues('CN2', 'CN10')).toBeLessThan(0);
    expect(compareValues(null, 0)).toBeLessThan(0);
    expect(compareValues('', null)).toBe(0);
  });
});

describe('withDefaultSorter', () => {
  const isAction = (c: { key?: unknown }) => c.key === 'actions';
  const [byName, byCode, withOwn, action, noIndex] =
    withDefaultSorter<Row>(
      [
        { title: 'Tên', dataIndex: 'name' },
        { title: 'Mã', dataIndex: ['info', 'code'] },
        { title: 'SL', dataIndex: 'qty', sorter: false },
        { title: 'Thao tác', key: 'actions', dataIndex: 'name' },
        { title: 'Khác', key: 'x' },
      ],
      isAction,
    ) ?? [];
  const a: Row = { name: 'An', qty: 1, info: { code: 'B2' } };
  const b: Row = { name: 'Bình', qty: 2, info: { code: 'B10' } };

  it('thêm sorter cho cột có dataIndex, kể cả dataIndex lồng nhau', () => {
    const sortName = (byName as { sorter: (x: Row, y: Row) => number }).sorter;
    const sortCode = (byCode as { sorter: (x: Row, y: Row) => number }).sorter;

    expect(sortName(a, b)).toBeLessThan(0);
    expect(sortCode(a, b)).toBeLessThan(0);
  });

  it('giữ nguyên cột đã khai báo sorter, cột thao tác và cột không có dataIndex', () => {
    expect((withOwn as { sorter: unknown }).sorter).toBe(false);
    expect('sorter' in (action as object)).toBe(false);
    expect('sorter' in (noIndex as object)).toBe(false);
  });
});

describe('toServerSort', () => {
  it('đổi kết quả antd sang sortBy / sortDir, bỏ sắp xếp thì trống', () => {
    expect(toServerSort({ field: 'orderCode', order: 'ascend' })).toEqual({
      sortBy: 'orderCode',
      sortDir: 'asc',
    });
    expect(toServerSort([{ field: 'qty', order: 'descend' }])).toEqual({
      sortBy: 'qty',
      sortDir: 'desc',
    });
    expect(toServerSort({ field: 'qty', order: undefined })).toEqual({});
  });
});

describe('withServerSorter', () => {
  const isAction = (c: { key?: unknown }) => c.key === 'actions';

  it('chỉ cột có dataIndex dạng chữ sắp xếp được, cột đang sắp mang sortOrder', () => {
    const [name, nested, action] =
      withServerSorter<Row>(
        [
          { title: 'Tên', dataIndex: 'name' },
          { title: 'Mã', dataIndex: ['info', 'code'] },
          { title: 'Thao tác', key: 'actions', dataIndex: 'qty' },
        ],
        { sortBy: 'Name', sortDir: 'desc' },
        isAction,
      ) ?? [];

    expect(name).toMatchObject({ sorter: true, sortOrder: 'descend' });
    expect('sorter' in (nested as object)).toBe(false);
    expect('sorter' in (action as object)).toBe(false);
  });

  it('cột ghép không có dataIndex chỉ sắp khi khai sorter: true, lấy key làm tên cột', () => {
    const [joined, plain] =
      withServerSorter<Row>(
        [
          { title: 'NVL', key: 'materialCode', sorter: true },
          { title: 'Khác', key: 'other' },
        ],
        { sortBy: 'materialCode', sortDir: 'asc' },
        isAction,
      ) ?? [];

    expect(joined).toMatchObject({ sorter: true, sortOrder: 'ascend' });
    expect('sorter' in (plain as object)).toBe(false);
    expect(toServerSort({ columnKey: 'materialCode', order: 'descend' })).toEqual({
      sortBy: 'materialCode',
      sortDir: 'desc',
    });
  });
});
