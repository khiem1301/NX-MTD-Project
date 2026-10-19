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

export type ApiError = {
  success: false;
  errorCode: string;
  message: string;
  errors?: Record<string, string[]>;
};
