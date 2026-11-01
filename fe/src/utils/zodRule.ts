import type { ZodType } from 'zod';

/** Dùng schema Zod làm rule của `Form.Item` (Rules 4.9); ô trống được kiểm như chuỗi rỗng. */
export const zodRule = (schema: ZodType) => ({
  validator: async (_: unknown, value: unknown) => {
    const result = schema.safeParse(value ?? '');
    if (!result.success) throw new Error(result.error.issues[0]?.message);
  },
});
