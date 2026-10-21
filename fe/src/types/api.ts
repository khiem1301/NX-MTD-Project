export type ApiResponse<T> = {
  success: true;
  data: T;
  message: string | null;
};

export type Paginated<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type PageParams = { page: number; pageSize: number; keyword?: string; sortBy?: string; sortDir?: 'asc' | 'desc' };
export const DEFAULT_PAGE_SIZE = 20;

export type ApiError = {
  success: false;
  errorCode: string;
  message: string;
  errors?: Record<string, string[]>;
};
