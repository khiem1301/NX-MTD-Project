export const ERROR_CODES = {
  AuthInvalidCredentials: 'AUTH_INVALID_CREDENTIALS',
  AuthAccountLocked: 'AUTH_ACCOUNT_LOCKED',
  AuthAccountDisabled: 'AUTH_ACCOUNT_DISABLED',
  AuthInvalidRefreshToken: 'AUTH_INVALID_REFRESH_TOKEN',
  AuthUnauthorized: 'AUTH_UNAUTHORIZED',
  AuthForbidden: 'AUTH_FORBIDDEN',
  CommonValidation: 'COMMON_VALIDATION',
  CommonNotFound: 'COMMON_NOT_FOUND',
  CommonConcurrencyConflict: 'COMMON_CONCURRENCY_CONFLICT',
  CommonInternalError: 'COMMON_INTERNAL_ERROR',
  CommonNetworkError: 'COMMON_NETWORK_ERROR',
} as const;
