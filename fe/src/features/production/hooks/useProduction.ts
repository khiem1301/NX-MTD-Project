import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { lookupApi } from '@/services/lookupApi';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { useOperationRole } from './useOperationRole';
import type {
  AddOrderLineTeamsPayload,
  SetOrderLineTeamsPayload,
  AssignManyPayload,
  AssignPayload,
  CancelDefectPayload,
  ConfirmDefectPayload,
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
  SetOperationsGroupPayload,
  OrderLineTeamParams,
  SaveOutputPayload,
  SavePieceRatePayload,
  UpdateQcBatchPayload,
} from '../types';

export const PRODUCTION_KEY = ['production'] as const;
const operationsKey = [...PRODUCTION_KEY, 'operations'] as const;
const pieceRatesKey = [...PRODUCTION_KEY, 'piece-rates'] as const;
const assignmentsKey = [...PRODUCTION_KEY, 'assignments'] as const;
const orderLineTeamsKey = [...PRODUCTION_KEY, 'order-line-teams'] as const;
const outputKey = [...PRODUCTION_KEY, 'output'] as const;
const qcKey = [...PRODUCTION_KEY, 'qc'] as const;
const defectsKey = [...PRODUCTION_KEY, 'defects'] as const;

// ---------- Công đoạn ----------

/** Lấy cả công đoạn đã ngừng; màn hình tự lọc theo trạng thái. */
export const useOperations = (productId: number | null) => {
  const role = useOperationRole();
  return useQuery({
    queryKey: [...operationsKey, productId],
    queryFn: () => productionApi.listOperations(role, productId!, true),
    enabled: productId !== null,
  });
};

export const useSaveOperation = (id: number | null) => {
  const role = useOperationRole();
  return useApiMutation(
    /** Trả mã công đoạn (tạo mới: mã vừa tạo) để màn hình mở tiếp ô nhập đơn giá. */
    async (body: SaveOperationPayload): Promise<number> => {
      if (id === null) return productionApi.createOperation(role, body);
      await productionApi.updateOperation(role, id, body);
      return id;
    },
    { invalidate: [operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );
};

/** Xem trước file Excel quy trình công nghệ: không ghi DB nên không invalidate. */
export const usePreviewOperationImport = () => {
  const role = useOperationRole();
  return useApiMutation(
    ({
      file,
      ...params
    }: {
      file: File;
      productId: number;
      sheetName?: string;
      effectiveDate?: string;
    }) => productionApi.previewOperationImport(role, file, params),
    { invalidate: [] },
  );
};

export const useImportOperations = () => {
  const role = useOperationRole();
  return useApiMutation(
    ({ file, params }: { file: File; params: OperationImportParams }) =>
      productionApi.importOperations(role, file, params),
    { invalidate: [operationsKey, pieceRatesKey] },
  );
};

export const useToggleOperation = () => {
  const role = useOperationRole();
  return useApiMutation(
    ({ id, discontinue }: { id: number; discontinue: boolean }) =>
      discontinue
        ? productionApi.discontinueOperation(role, id)
        : productionApi.reactivateOperation(role, id),
    { invalidate: [operationsKey, pieceRatesKey], successMessage: PRODUCTION_TEXT.saved },
  );
};

// ---------- Nhóm công đoạn ----------

/** Nằm dưới khóa công đoạn: lưu công đoạn có thể tạo nhóm mới, đổi nhóm thì danh sách công đoạn cũng đổi. */
export const useOperationGroups = (productId: number | null) => {
  const role = useOperationRole();
  return useQuery({
    queryKey: [...operationsKey, 'groups', productId],
    queryFn: () => productionApi.listOperationGroups(role, productId!),
    enabled: productId !== null,
  });
};

export const useCreateOperationGroup = () => {
  const role = useOperationRole();
  return useApiMutation(
    ({ productId, groupName }: { productId: number; groupName: string }) =>
      productionApi.createOperationGroup(role, productId, groupName),
    { invalidate: [operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );
};

export const useRenameOperationGroup = () => {
  const role = useOperationRole();
  return useApiMutation(
    ({ id, groupName }: { id: number; groupName: string }) =>
      productionApi.renameOperationGroup(role, id, groupName),
    { invalidate: [operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );
};

export const useDeleteOperationGroup = () => {
  const role = useOperationRole();
  return useApiMutation((id: number) => productionApi.deleteOperationGroup(role, id), {
    invalidate: [operationsKey],
    successMessage: PRODUCTION_TEXT.saved,
  });
};

export const useSetOperationsGroup = () => {
  const role = useOperationRole();
  return useApiMutation(
    (body: SetOperationsGroupPayload) => productionApi.setOperationsGroup(role, body),
    { invalidate: [operationsKey] },
  );
};

// ---------- Đơn giá ----------

export const usePieceRates = (role: PieceRateRole, query: PieceRateQuery) =>
  useQuery({
    queryKey: [...pieceRatesKey, role, query],
    queryFn: () => productionApi.listPieceRates(role, query),
  });

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

export const useSubmitPieceRates = () =>
  useApiMutation((ids: number[]) => productionApi.submitPieceRates(ids), {
    invalidate: [pieceRatesKey, operationsKey],
  });

export const useApprovePieceRate = () =>
  useApiMutation(
    ({ id, note }: { id: number; note?: string }) => productionApi.approvePieceRate(id, note),
    {
      invalidate: [pieceRatesKey, operationsKey],
      successMessage: PRODUCTION_TEXT.pieceRates.done.approved,
    },
  );

export const useApproveManyPieceRates = () =>
  useApiMutation(
    ({ ids, note }: { ids: number[]; note?: string }) =>
      productionApi.approveManyPieceRates(ids, note),
    {
      invalidate: [pieceRatesKey, operationsKey],
    },
  );

export const useRejectPieceRate = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => productionApi.rejectPieceRate(id, reason),
    {
      invalidate: [pieceRatesKey],
      successMessage: PRODUCTION_TEXT.pieceRates.done.rejected,
    },
  );

// ---------- Chia mã hàng của đơn cho tổ may ----------

export const useOrderLineTeams = (params: OrderLineTeamParams) =>
  useQuery({
    queryKey: [...orderLineTeamsKey, params],
    queryFn: () => productionApi.listOrderLineTeams(params),
  });

export const useSetOrderLineTeams = () =>
  useApiMutation((body: SetOrderLineTeamsPayload) => productionApi.setOrderLineTeams(body), {
    invalidate: [orderLineTeamsKey],
    successMessage: PRODUCTION_TEXT.orderLineTeams.saved,
  });

export const useAddOrderLineTeams = () =>
  useApiMutation((body: AddOrderLineTeamsPayload) => productionApi.addOrderLineTeams(body), {
    invalidate: [orderLineTeamsKey],
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

/** Dòng đơn của các mã hàng được chia cho tổ, để biết cả mã chưa có công đoạn. Không dùng cache tra cứu: việc chia có thể vừa đổi. */
export const useAllottedOrderLines = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'order-lines', orderId],
    queryFn: () => lookupApi.orderLines(orderId!),
    enabled: orderId !== null,
  });

export const useAssignOperation = () =>
  useApiMutation((body: AssignPayload) => productionApi.assign(body), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.assigned,
  });

export const useAssignOperations = () =>
  useApiMutation((body: AssignManyPayload) => productionApi.assignMany(body), {
    invalidate: [assignmentsKey, outputKey],
  });

export const useCloseAssignment = () =>
  useApiMutation((id: number) => productionApi.closeAssignment(id), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.closed,
  });

type SaveWorkerOperationsPayload = {
  orderId: number;
  workerId: number;
  addOperationIds: number[];
  closeAssignmentIds: number[];
};

/** Lưu một lần phần thay đổi công đoạn của một công nhân: thêm theo lô, rồi đóng từng phân công bị bỏ tích. */
export const useSaveWorkerOperations = () =>
  useApiMutation(
    async ({ orderId, workerId, addOperationIds, closeAssignmentIds }: SaveWorkerOperationsPayload) => {
      if (addOperationIds.length > 0)
        await productionApi.assignMany({ orderId, workerId, operationIds: addOperationIds });
      for (const id of closeAssignmentIds) await productionApi.closeAssignment(id);
    },
    {
      invalidate: [assignmentsKey, outputKey],
      successMessage: PRODUCTION_TEXT.assignments.byWorker.saved,
    },
  );

export const useCopyAssignments = () =>
  useApiMutation((body: CopyAssignmentsPayload) => productionApi.copyAssignments(body), {
    invalidate: [assignmentsKey, outputKey],
  });

// ---------- Sản lượng ----------

export const useOutputGrid = (role: OutputRole, query: OutputGridQuery | null) =>
  useQuery({
    queryKey: [...outputKey, role, query],
    queryFn: () => productionApi.getOutputGrid(role, query!),
    enabled: query !== null,
  });

export const useSaveOutput = (role: OutputRole) =>
  useApiMutation((body: SaveOutputPayload) => productionApi.saveOutput(role, body), {
    invalidate: [outputKey],
  });

// ---------- QC ----------

export const useQcBatches = (role: QcRole, query: QcBatchQuery) =>
  useQuery({
    queryKey: [...qcKey, role, 'list', query],
    queryFn: () => productionApi.listQcBatches(role, query),
  });

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
  useApiMutation(
    ({ id, body }: { id: number; body: UpdateQcBatchPayload }) =>
      productionApi.updateQcBatch(role, id, body),
    {
      invalidate: [qcKey],
      successMessage: PRODUCTION_TEXT.qc.done.updated,
    },
  );

export const useDeleteQcBatch = (role: QcRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: DeleteQcBatchPayload }) =>
      productionApi.deleteQcBatch(role, id, body),
    {
      invalidate: [qcKey],
      successMessage: PRODUCTION_TEXT.qc.done.deleted,
    },
  );

// ---------- Hàng hỏng ----------

export const useDefects = (role: OutputRole, query: DefectQuery) =>
  useQuery({
    queryKey: [...defectsKey, role, 'list', query],
    queryFn: () => productionApi.listDefects(role, query),
  });

export const useCreateDefect = () =>
  useApiMutation((body: CreateDefectPayload) => productionApi.createDefect(body), {
    invalidate: [defectsKey],
    successMessage: PRODUCTION_TEXT.defects.done.created,
  });

export const useCancelDefect = (role: OutputRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: CancelDefectPayload }) =>
      productionApi.cancelDefect(role, id, body),
    {
      invalidate: [defectsKey],
      successMessage: PRODUCTION_TEXT.defects.done.cancelled,
    },
  );

/** Quản đốc / Admin chấp nhận phiếu sau khi công nhân phản đối (giữ phiếu); không chấp nhận thì hủy phiếu. */
export const useConfirmDefect = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmDefectPayload }) =>
      productionApi.confirmDefect(id, body),
    { invalidate: [defectsKey], successMessage: PRODUCTION_TEXT.defects.done.confirmed },
  );

export const useMyDefects = (query: DefectQuery) =>
  useQuery({
    queryKey: [...defectsKey, 'mine', query],
    queryFn: () => productionApi.listMyDefects(query),
  });

export const useObjectDefect = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ObjectDefectPayload }) =>
      productionApi.objectDefect(id, body),
    {
      invalidate: [defectsKey],
      successMessage: PRODUCTION_TEXT.myDefects.done,
    },
  );
