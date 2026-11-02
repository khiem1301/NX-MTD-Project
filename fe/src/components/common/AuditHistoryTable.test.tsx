import { render, screen } from '@testing-library/react';
import type { AuditEntry } from '@/types/attachment';
import { AuditHistoryTable } from './AuditHistoryTable';

const entry = (id: number, summary: string): AuditEntry => ({
  auditLogId: id,
  action: 'UPDATE',
  oldValues: null,
  newValues: null,
  reason: null,
  summary,
  changedBy: 1,
  changedByName: 'Quản đốc',
  changedAt: `2026-11-0${(id % 9) + 1}T08:00:00`,
});

describe('AuditHistoryTable', () => {
  it('mới nhất ở trên, hiện câu mô tả khi không có trường đổi giá trị', () => {
    render(
      <AuditHistoryTable history={[entry(1, 'Thêm vào tổ: An'), entry(2, 'Loại khỏi tổ: An')]} />,
    );
    const rows = screen.getAllByRole('row').slice(1);
    expect(rows[0]).toHaveTextContent('Loại khỏi tổ: An');
    expect(rows[1]).toHaveTextContent('Thêm vào tổ: An');
  });

  it('phân trang 10 dòng mỗi trang, có tổng số dòng', () => {
    const history = Array.from({ length: 25 }, (_, i) => entry(i + 1, `Dòng ${i + 1}`));
    render(<AuditHistoryTable history={history} />);
    expect(screen.getAllByRole('row')).toHaveLength(11);
    expect(screen.getByText('1–10 / 25 dòng')).toBeInTheDocument();
  });
});
