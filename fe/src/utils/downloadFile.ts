import { apiClient } from '@/services/apiClient';

/** GET file nhị phân (Excel) rồi cho trình duyệt tải về với tên `fileName`. */
export const downloadBlob = async (url: string, fileName: string, params?: object): Promise<void> => {
  const res = await apiClient.get<Blob>(url, { params, responseType: 'blob' });
  const objectUrl = window.URL.createObjectURL(new Blob([res.data]));
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(objectUrl);
};
