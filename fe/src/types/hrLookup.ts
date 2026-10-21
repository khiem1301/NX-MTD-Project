export type TeamLookup = { teamId: number; teamCode: string; teamName: string; teamType: 'Cutting' | 'Sewing' | 'Office' };

export type EmployeeLookup = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  role: string;
  teamId: number | null;
  payType: string;
};
