import { Alert, Button, Card, Form, Input, Typography } from 'antd';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { MESSAGES } from '@/constants/messages';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';
import type { LoginRequest } from '@/types/auth';
import { useLogin } from '../hooks/useLogin';

const loginSchema = z.object({
  username: z.string().trim().min(1, MESSAGES.auth.usernameRequired),
  password: z.string().min(1, MESSAGES.auth.passwordRequired),
});

const zodRule = (field: keyof LoginRequest) => ({
  validator: async (_: unknown, value: unknown) => {
    const result = loginSchema.shape[field].safeParse(value ?? '');
    if (!result.success) throw new Error(result.error.issues[0]?.message);
  },
});

const LOGIN_FIELDS = ['username', 'password'] as const;

const readFrom = (state: unknown): string => {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const from = (state as { from?: { pathname?: unknown } }).from;
    if (typeof from?.pathname === 'string') return from.pathname;
  }
  return ROUTES.home;
};

export function LoginPage() {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [form] = Form.useForm<LoginRequest>();
  const login = useLogin();

  if (isAuthenticated) return <Navigate to={ROUTES.home} replace />;

  const onFinish = (values: LoginRequest) => {
    login.mutate(
      { username: values.username.trim(), password: values.password },
      {
        onSuccess: () => navigate(readFrom(location.state), { replace: true }),
        onError: (error) => {
          const { errors } = error;
          if (errors) {
            form.setFields(
              LOGIN_FIELDS.flatMap((name) => {
                const fieldErrors = errors[name];
                return fieldErrors ? [{ name, errors: fieldErrors }] : [];
              }),
            );
          }
        },
      },
    );
  };

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 16 }}>
      <Card style={{ width: 380, maxWidth: '100%' }}>
        <Typography.Title level={3} style={{ marginBottom: 0 }}>
          {MESSAGES.auth.title}
        </Typography.Title>
        <Typography.Paragraph type="secondary">{MESSAGES.auth.subtitle}</Typography.Paragraph>
        <Form form={form} layout="vertical" onFinish={onFinish} disabled={login.isPending}>
          <Form.Item name="username" label={MESSAGES.auth.username} rules={[zodRule('username')]}>
            <Input autoFocus autoComplete="username" size="large" />
          </Form.Item>
          <Form.Item name="password" label={MESSAGES.auth.password} rules={[zodRule('password')]}>
            <Input.Password autoComplete="current-password" size="large" />
          </Form.Item>
          {login.isError && !login.error.errors && (
            <Alert
              type="error"
              showIcon
              message={login.error.message}
              style={{ marginBottom: 16 }}
            />
          )}
          <Button type="primary" htmlType="submit" size="large" block loading={login.isPending}>
            {MESSAGES.auth.loginButton}
          </Button>
        </Form>
      </Card>
    </div>
  );
}
