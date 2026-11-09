/** Query key của luồng sản xuất: mọi khóa nằm dưới PRODUCTION_KEY để invalidate cả luồng được một lần. */
export const PRODUCTION_KEY = ['production'] as const;
export const operationsKey = [...PRODUCTION_KEY, 'operations'] as const;
export const pieceRatesKey = [...PRODUCTION_KEY, 'piece-rates'] as const;
export const assignmentsKey = [...PRODUCTION_KEY, 'assignments'] as const;
export const orderLineTeamsKey = [...PRODUCTION_KEY, 'order-line-teams'] as const;
export const outputKey = [...PRODUCTION_KEY, 'output'] as const;
export const qcKey = [...PRODUCTION_KEY, 'qc'] as const;
export const defectsKey = [...PRODUCTION_KEY, 'defects'] as const;
