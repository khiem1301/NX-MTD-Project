import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  AddOrderLineTeamsPayload,
  SetOrderLineTeamsPayload,
  AssignManyPayload,
  AssignPayload,
  CancelDefectPayload,
  CopyAssignmentsPayload,
  CreateDefectPayload,
  CreateQcBatchPayload,
  DeleteQcBatchPayload,
  Defect,
  DefectQuery,
  OnLineQty,
  Operation,
  OperationGroup,
  OperationRole,
  SetOperationsGroupPayload,
  OperationImportParams,
  OperationImportPreview,
  OperationImportResult,
  OperationAssignment,
  OrderLineTeamParams,
  OrderLineTeamRow,
  OperationOption,
  OutputGrid,
  OutputGridQuery,
  OutputRole,
  OutputSaveResult,
  UpdateDefectPayload,
  PieceRate,
  PieceRateQuery,
  PieceRateRole,
  QcBatch,
  QcBatchQuery,
  QcRole,
  SaveOperationPayload,
  SaveOutputPayload,
  SavePieceRatePayload,
  UpdateQcBatchPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown, params?: object): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body, { params })).data.data;

const asFormData = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};
const del = async <T>(url: string): Promise<T> =>
  (await apiClient.delete<ApiResponse<T>>(url)).data.data;

const put = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

const P = API.production;

export const productionApi = {
  // ---------- KI-01: công đoạn và nhóm công đoạn (Kế toán, Admin) ----------
  listOperations: (role: OperationRole, productId: number, includeDiscontinued: boolean) =>
    get<Operation[]>(P.operations(role), { productId, includeDiscontinued }),
  previewOperationImport: (
    role: OperationRole,
    file: File,
    params: Pick<OperationImportParams, 'productId' | 'sheetName' | 'effectiveDate'>,
  ) => post<OperationImportPreview>(P.operationImportPreview(role), asFormData(file), params),
  importOperations: (role: OperationRole, file: File, params: OperationImportParams) =>
    post<OperationImportResult>(P.operationImport(role), asFormData(file), params),
  createOperation: (role: OperationRole, body: SaveOperationPayload) =>
    post<number>(P.operations(role), body),
  updateOperation: (role: OperationRole, id: number, body: SaveOperationPayload) =>
    put<void>(P.operation(role, id), body),
  discontinueOperation: (role: OperationRole, id: number) =>
    post<void>(P.operationDiscontinue(role, id)),
  reactivateOperation: (role: OperationRole, id: number) =>
    post<void>(P.operationReactivate(role, id)),
  listOperationGroups: (role: OperationRole, productId: number) =>
    get<OperationGroup[]>(P.operationGroups(role), { productId }),
  createOperationGroup: (role: OperationRole, productId: number, groupName: string) =>
    post<number>(P.operationGroups(role), { productId, groupName }),
  renameOperationGroup: (role: OperationRole, id: number, groupName: string) =>
    put<void>(P.operationGroup(role, id), { groupName }),
  deleteOperationGroup: (role: OperationRole, id: number) => del<void>(P.operationGroup(role, id)),
  setOperationsGroup: (role: OperationRole, body: SetOperationsGroupPayload) =>
    put<number>(P.operationGroupMembers(role), body),

  // ---------- KI-01: đơn giá ----------
  listPieceRates: (role: PieceRateRole, query: PieceRateQuery) =>
    get<Paginated<PieceRate>>(P.pieceRates(role), query),
  createPieceRate: (body: SavePieceRatePayload) => post<number>(P.pieceRates('accountant'), body),
  updatePieceRate: (id: number, body: SavePieceRatePayload) => put<void>(P.pieceRate(id), body),
  submitPieceRates: (ids: number[]) => post<number>(P.pieceRateSubmitMany, { ids }),
  submitPieceRate: (id: number) => post<void>(P.pieceRateSubmit(id)),
  approvePieceRate: (id: number, note?: string) => post<void>(P.pieceRateApprove(id), { note }),
  approveManyPieceRates: (ids: number[], note?: string) =>
    post<number>(P.pieceRateApproveMany, { ids, note }),
  rejectPieceRate: (id: number, reason: string) => post<void>(P.pieceRateReject(id), { reason }),

  // ---------- KI-03: phân công (tổ trưởng / tổ phó) ----------
  listOrderLineTeams: (params: OrderLineTeamParams) =>
    get<Paginated<OrderLineTeamRow>>(P.orderLineTeams, params),
  setOrderLineTeams: (body: SetOrderLineTeamsPayload) => put<void>(P.orderLineTeams, body),
  addOrderLineTeams: (body: AddOrderLineTeamsPayload) => post<number>(P.orderLineTeamsBulk, body),
  listAssignments: (orderId: number) => get<OperationAssignment[]>(P.assignments, { orderId }),
  listAssignableOperations: (orderId: number) =>
    get<OperationOption[]>(P.assignmentOperations, { orderId }),
  assign: (body: AssignPayload) => post<number>(P.assignments, body),
  assignMany: (body: AssignManyPayload) => post<number>(P.assignmentsBulk, body),
  closeAssignment: (id: number) => post<void>(P.assignmentClose(id)),
  copyAssignments: (body: CopyAssignmentsPayload) => post<number>(P.assignmentCopy, body),

  // ---------- KI-04: sản lượng cuối ca ----------
  getOutputGrid: (role: OutputRole, query: OutputGridQuery) =>
    get<OutputGrid>(P.outputGrid(role), query),
  saveOutput: (role: OutputRole, body: SaveOutputPayload) =>
    post<OutputSaveResult>(P.output(role), body),

  // ---------- KI-06: QC ----------
  listQcBatches: (role: QcRole, query: QcBatchQuery) =>
    get<Paginated<QcBatch>>(P.qcBatches(role), query),
  getOnLine: (sewingTeamId: number, orderLineId: number) =>
    get<OnLineQty>(P.qcOnLine, { sewingTeamId, orderLineId }),
  createQcBatch: (body: CreateQcBatchPayload) => post<number>(P.qcBatches('qa-qc'), body),
  updateQcBatch: (role: QcRole, id: number, body: UpdateQcBatchPayload) =>
    put<void>(P.qcBatch(role, id), body),
  deleteQcBatch: (role: QcRole, id: number, body: DeleteQcBatchPayload) =>
    post<void>(P.qcBatchDelete(role, id), body),

  // ---------- KI-08: hàng hỏng ----------
  listDefects: (role: OutputRole, query: DefectQuery) =>
    get<Paginated<Defect>>(P.defects(role), query),
  createDefect: (body: CreateDefectPayload) => post<number>(P.defects('team-leader'), body),
  cancelDefect: (role: OutputRole, id: number, body: CancelDefectPayload) =>
    post<void>(P.defectCancel(role, id), body),
  updateDefect: (role: OutputRole, id: number, body: UpdateDefectPayload) =>
    put<void>(P.defectUpdate(role, id), body),
  listMyDefects: (query: DefectQuery) => get<Defect[]>(P.myDefects, query),
};
