import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { operationsKey, pieceRatesKey } from './productionKeys';
import { useOperationRole } from './useOperationRole';
import type {
  OperationImportParams,
  SaveOperationPayload,
  SetOperationsGroupPayload,
} from '../types';

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
