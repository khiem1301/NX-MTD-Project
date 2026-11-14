import { fireEvent, render, screen } from '@testing-library/react';
import { KPI_TEXT } from '../constants';
import type { EndOfOrderOperation } from '../types';
import { operationMatches } from '../utils/mismatch';
import { MismatchDetail } from './MismatchDetail';

const worker = (id: number, name: string, qty: number) => ({
  workerId: id,
  workerCode: `CN${id}`,
  workerName: name,
  reportedQty: qty,
});

const OPERATIONS: EndOfOrderOperation[] = [
  {
    operationId: 1,
    operationCode: 'OP10',
    operationName: 'May cổ',
    mismatchQty: 15,
    lines: [
      {
        orderLineId: 11,
        color: 'Đen',
        sizeCode: 'M',
        reportedQty: 100,
        qcQty: 85,
        scrapQty: 0,
        mismatchQty: 15,
        workers: [worker(7, 'Lan', 60), worker(8, 'Hùng', 40)],
      },
    ],
  },
  {
    operationId: 2,
    operationCode: 'OPF',
    operationName: 'Hoàn thiện',
    mismatchQty: 5,
    lines: [
      {
        orderLineId: 11,
        color: 'Đen',
        sizeCode: 'M',
        reportedQty: 90,
        qcQty: 85,
        scrapQty: 0,
        mismatchQty: 5,
        workers: [worker(9, 'Mai', 90)],
      },
    ],
  },
];

describe('MismatchDetail', () => {
  it('không có công đoạn lệch thì chỉ hiện thông báo không lệch', () => {
    render(<MismatchDetail operations={[]} emptyText="Không lệch" />);

    expect(screen.getByText('Không lệch')).toBeInTheDocument();
    expect(screen.queryByPlaceholderText(KPI_TEXT.finish.search)).not.toBeInTheDocument();
  });

  it('hiện từng công đoạn lệch kèm số dòng đơn và số lệch, theo thứ tự BE trả về', () => {
    render(<MismatchDetail operations={OPERATIONS} emptyText="" />);

    expect(screen.getAllByText(/OP10 - May cổ|OPF - Hoàn thiện/).map((e) => e.textContent)).toEqual(
      ['OP10 - May cổ', 'OPF - Hoàn thiện'],
    );
    expect(screen.getAllByText('15').length).toBeGreaterThan(0);
  });

  it('tìm theo tên công nhân thì chỉ giữ công đoạn có người đó đã báo', () => {
    render(<MismatchDetail operations={OPERATIONS} emptyText="" />);

    fireEvent.change(screen.getByPlaceholderText(KPI_TEXT.finish.search), {
      target: { value: 'mai' },
    });

    expect(screen.queryByText('OP10 - May cổ')).not.toBeInTheDocument();
    expect(screen.getByText('OPF - Hoàn thiện')).toBeInTheDocument();
  });
});

describe('operationMatches', () => {
  const [collar] = OPERATIONS as [EndOfOrderOperation];

  it('khớp theo mã, tên công đoạn hoặc mã, tên công nhân, không phân biệt hoa thường', () => {
    expect(operationMatches(collar, '')).toBe(true);
    expect(operationMatches(collar, 'op10')).toBe(true);
    expect(operationMatches(collar, 'MAY CỔ')).toBe(true);
    expect(operationMatches(collar, 'hùng')).toBe(true);
    expect(operationMatches(collar, 'cn8')).toBe(true);
    expect(operationMatches(collar, 'mai')).toBe(false);
  });
});
