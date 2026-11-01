import { useQuery } from '@tanstack/react-query';
import { warehouseApi } from '../api/warehouseApi';
import { WAREHOUSE_KEY } from './useMaterials';

/** NVL chờ nhận của Thủ kho; nằm dưới WAREHOUSE_KEY nên tự nạp lại sau khi lập / xác nhận phiếu nhập. */
export const useIncomingMaterials = () =>
  useQuery({
    queryKey: [...WAREHOUSE_KEY, 'incoming-materials'],
    queryFn: warehouseApi.getIncomingMaterials,
  });
