import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import type {
  AssignPayload,
  CancelDefectPayload,
  CopyAssignmentsPayload,
  CreateDefectPayload,
  CreateQcBatchPayload,
  DeleteQcBatchPayload,
  DefectQuery,
  ObjectDefectPayload,
  OperationImportParams,
  OutputGridQuery,
  OutputRole,
  PieceRateQuery,
  PieceRateRole,
  QcBatchQuery,
  QcRole,
  SaveOperationPayload,
  SaveOutputPayload,
  SavePieceRatePayload,
  UpdateQcBatchPayload,
} from '../types';

export const PRODUCTION_KEY = ['production'] as const;
const operationsKey = [...PRODUCTION_KEY, 'operations'] as const;
const pieceRatesKey = [...PRODUCTION_KEY, 'piece-rates'] as const;
const assignmentsKey = [...PRODUCTION_KEY, 'assignments'] as const;
const outputKey = [...PRODUCTION_KEY, 'output'] as const;
const qcKey = [...PRODUCTION_KEY, 'qc'] as const;
const defectsKey = [...PRODUCTION_KEY, 'defects'] as const;

// ---------- Công đoạn ----------

export const useOperations = (productId: number | null, includeDiscontinued = false) =>
  useQuery({
    queryKey: [...operationsKey, productId, includeDiscontinued],
    queryFn: () => productionApi.listOperations(productId!, includeDiscontinued),
    enabled: productId !== null,
  });

export const useSaveOperation = (id: number | null) =>
  useApiMutation(
    async (body: SaveOperationPayload): Promise<void> => {
      if (id === null) await productionApi.createOperation(body);
      else await productionApi.updateOperation(id, body);
    },
    { invalidate: [operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );

/** Xem trước file Excel quy trình công nghệ: không ghi DB nên không invalidate. */
export const usePreviewOperationImport = () =>
  useApiMutation(
    ({
      file,
      ...params
    }: {
      file: File;
      productId: number;
      sheetName?: string;
      effectiveDate?: string;
    }) => productionApi.previewOperationImport(file, params),
    { invalidate: [] },
  );

export const useImportOperations = () =>
  useApiMutation(
    ({ file, params }: { file: File; params: OperationImportParams }) => productionApi.importOperations(file, params),
    { invalidate: [operationsKey, pieceRatesKey] },
  );

export const useToggleOperation = () =>
  useApiMutation(
    ({ id, discontinue }: { id: number; discontinue: boolean }) =>
      discontinue ? productionApi.discontinueOperation(id) : productionApi.reactivateOperation(id),
    { invalidate: [operationsKey, pieceRatesKey], successMessage: PRODUCTION_TEXT.saved },
  );

// ---------- Đơn giá ----------

export const usePieceRates = (role: PieceRateRole, query: PieceRateQuery) =>
  useQuery({ queryKey: [...pieceRatesKey, role, query], queryFn: () => productionApi.listPieceRates(role, query) });

export const useSavePieceRate = (id: number | null) =>
  useApiMutation(
    async (body: SavePieceRatePayload): Promise<void> => {
      if (id === null) await productionApi.createPieceRate(body);
      else await productionApi.updatePieceRate(id, body);
    },
    { invalidate: [pieceRatesKey, operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );

export const useSubmitPieceRate = () =>
  useApiMutation((id: number) => productionApi.submitPieceRate(id), {
    invalidate: [pieceRatesKey, operationsKey],
    successMessage: PRODUCTION_TEXT.pieceRates.done.submitted,
  });

export const useApprovePieceRate = () =>
  useApiMutation(({ id, note }: { id: number; note?: string }) => productionApi.approvePieceRate(id, note), {
    invalidate: [pieceRatesKey, operationsKey],
    successMessage: PRODUCTION_TEXT.pieceRates.done.approved,
  });

export const useApproveManyPieceRates = () =>
  useApiMutation(({ ids, note }: { ids: number[]; note?: string }) => productionApi.approveManyPieceRates(ids, note), {
    invalidate: [pieceRatesKey, operationsKey],
  });

export const useRejectPieceRate = () =>
  useApiMutation(({ id, reason }: { id: number; reason: string }) => productionApi.rejectPieceRate(id, reason), {
    invalidate: [pieceRatesKey],
    successMessage: PRODUCTION_TEXT.pieceRates.done.rejected,
  });

// ---------- Phân công ----------

export const useAssignments = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'list', orderId],
    queryFn: () => productionApi.listAssignments(orderId!),
    enabled: orderId !== null,
  });

/** Công đoạn may đang dùng của các mã hàng trong đơn (chọn khi phân công, ghi hàng hỏng). */
export const useAssignableOperations = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'operations', orderId],
    queryFn: () => productionApi.listAssignableOperations(orderId!),
    enabled: orderId !== null,
  });

export const useAssignOperation = () =>
  useApiMutation((body: AssignPayload) => productionApi.assign(body), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.assigned,
  });

export const useReplaceAssignment = () =>
  useApiMutation(
    ({ id, newWorkerId }: { id: number; newWorkerId: number }) => productionApi.replaceAssignment(id, newWorkerId),
    { invalidate: [assignmentsKey, outputKey], successMessage: PRODUCTION_TEXT.assignments.done.replaced },
  );

export const useCloseAssignment = () =>
  useApiMutation((id: number) => productionApi.closeAssignment(id), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.closed,
  });

export const useCopyAssignments = () =>
  useApiMutation((body: CopyAssignmentsPayload) => productionApi.copyAssignments(body), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.copied,
  });

// ---------- Sản lượng ----------

export const useOutputGrid = (role: OutputRole, query: OutputGridQuery | null) =>
  useQuery({
    queryKey: [...outputKey, role, query],
    queryFn: () => productionApi.getOutputGrid(role, query!),
    enabled: query !== null,
  });

export const useSaveOutput = (role: OutputRole) =>
  useApiMutation((body: SaveOutputPayload) => productionApi.saveOutput(role, body), { invalidate: [outputKey] });

// ---------- QC ----------

export const useQcBatches = (role: QcRole, query: QcBatchQuery) =>
  useQuery({ queryKey: [...qcKey, role, 'list', query], queryFn: () => productionApi.listQcBatches(role, query) });

export const useOnLineQty = (sewingTeamId: number | null, orderLineId: number | null) =>
  useQuery({
    queryKey: [...qcKey, 'on-line', sewingTeamId, orderLineId],
    queryFn: () => productionApi.getOnLine(sewingTeamId!, orderLineId!),
    enabled: sewingTeamId !== null && orderLineId !== null,
  });

export const useCreateQcBatch = () =>
  useApiMutation((body: CreateQcBatchPayload) => productionApi.createQcBatch(body), {
    invalidate: [qcKey],
    successMessage: PRODUCTION_TEXT.qc.done.created,
  });

export const useUpdateQcBatch = (role: QcRole) =>
  useApiMutation(({ id, body }: { id: number; body: UpdateQcBatchPayload }) => productionApi.updateQcBatch(role, id, body), {
    invalidate: [qcKey],
    successMessage: PRODUCTION_TEXT.qc.done.updated,
  });

export const useDeleteQcBatch = (role: QcRole) =>
  useApiMutation(({ id, body }: { id: number; body: DeleteQcBatchPayload }) => productionApi.deleteQcBatch(role, id, body), {
    invalidate: [qcKey],
    successMessage: PRODUCTION_TEXT.qc.done.deleted,
  });

// ---------- Hàng hỏng ----------

export const useDefects = (role: OutputRole, query: DefectQuery) =>
  useQuery({ queryKey: [...defectsKey, role, 'list', query], queryFn: () => productionApi.listDefects(role, query) });

export const useCreateDefect = () =>
  useApiMutation((body: CreateDefectPayload) => productionApi.createDefect(body), {
    invalidate: [defectsKey],
    successMessage: PRODUCTION_TEXT.defects.done.created,
  });

export const useCancelDefect = (role: OutputRole) =>
  useApiMutation(({ id, body }: { id: number; body: CancelDefectPayload }) => productionApi.cancelDefect(role, id, body), {
    invalidate: [defectsKey],
    successMessage: PRODUCTION_TEXT.defects.done.cancelled,
  });

export const useMyDefects = (query: DefectQuery) =>
  useQuery({ queryKey: [...defectsKey, 'mine', query], queryFn: () => productionApi.listMyDefects(query) });

export const useObjectDefect = () =>
  useApiMutation(({ id, body }: { id: number; body: ObjectDefectPayload }) => productionApi.objectDefect(id, body), {
    invalidate: [defectsKey],
    successMessage: PRODUCTION_TEXT.myDefects.done,
  });
