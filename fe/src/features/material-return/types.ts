import type { PageParams } from '@/types/api';

export type MaterialReturnListItem = {
  materialReturnId: number;
  returnCode: string;
  materialIssueId: number;
  issueCode: string;
  orderCode: string;
  cuttingTeamId: number;
  cuttingTeamName: string;
  status: string;
  createdAt: string;
  confirmedByName: string | null;
  confirmedAt: string | null;
  totalReturnedQty: number;
};

export type MaterialReturnLine = {
  materialReturnLineId: number;
  materialIssueLineId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  returnedQty: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
};

export type MaterialReturn = Omit<MaterialReturnListItem, 'totalReturnedQty'> & {
  note: string | null;
  rejectReason: string | null;
  lines: MaterialReturnLine[];
};

export type ReturnableLine = {
  materialIssueId: number;
  issueCode: string;
  materialIssueLineId: number;
  orderCode: string;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  receivedQty: number;
  returnedQty: number;
  remainingQty: number;
};

export type MaterialReturnQuery = PageParams & { status?: string };

export type CreateReturnPayload = {
  materialIssueId: number;
  note?: string;
  lines: { materialIssueLineId: number; returnedQty: number }[];
};

export type ConfirmReturnPayload = {
  lines: { materialReturnLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};
