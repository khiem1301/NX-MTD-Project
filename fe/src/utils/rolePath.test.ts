import { ROLES } from '@/constants/roles';
import { rolePathOf } from './rolePath';

describe('rolePathOf', () => {
  it('map role sang tiền tố URL BE', () => {
    expect(rolePathOf(ROLES.WarehouseKeeper)).toBe('warehouse-keeper');
    expect(rolePathOf(ROLES.Manager)).toBe('manager');
    expect(rolePathOf(ROLES.Admin)).toBe('admin');
  });

  it('tổ phó dùng API của tổ trưởng', () => {
    expect(rolePathOf(ROLES.TeamDeputy)).toBe('team-leader');
    expect(rolePathOf(ROLES.TeamLeader)).toBe('team-leader');
  });

  it('role không dùng màn kho thì trả null', () => {
    expect(rolePathOf(ROLES.Worker)).toBeNull();
    expect(rolePathOf(undefined)).toBeNull();
  });
});
