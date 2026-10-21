import { render, screen } from '@testing-library/react';
import type { AuditEntry } from '@/types/attachment';
import { RequestHistory } from './RequestHistory';

const entry = (patch: Partial<AuditEntry>): AuditEntry => ({
  auditLogId: 1,
  action: 'UPDATE',
  oldValues: null,
  newValues: null,
  reason: null,
  changedBy: 2,
  changedByName: 'Admin',
  changedAt: '2026-10-22T09:00:00+07:00',
  ...patch,
});

const renderHistory = (history: AuditEntry[]) =>
  render(<RequestHistory createdByName="Cơ điện" createdAt="2026-10-22T08:00:00+07:00" history={history} />);

describe('RequestHistory', () => {
  it('luôn có dòng "Tạo đơn" đầu tiên lấy từ chính đơn', () => {
    renderHistory([]);
    expect(screen.getByText('Tạo đơn')).toBeInTheDocument();
    expect(screen.getByText('Cơ điện · 22/10/2026 08:00')).toBeInTheDocument();
  });

  it('dòng từ chối hiện lý do', () => {
    renderHistory([entry({ action: 'REJECT', reason: 'Dùng đồ dự phòng' })]);
    expect(screen.getByText('Từ chối')).toBeInTheDocument();
    expect(screen.getByText('Lý do: Dùng đồ dự phòng')).toBeInTheDocument();
  });

  it('UPDATE đổi Status sang CANCELLED hiện "Hủy đơn"', () => {
    renderHistory([entry({ newValues: '{"Status":"CANCELLED"}' })]);
    expect(screen.getByText('Hủy đơn')).toBeInTheDocument();
  });

  it('newValues không phải JSON thì không làm vỡ giao diện', () => {
    renderHistory([entry({ newValues: 'không phải json' })]);
    expect(screen.getByText('Cập nhật')).toBeInTheDocument();
  });
});
