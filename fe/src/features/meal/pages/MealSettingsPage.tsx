import { useEffect } from 'react';
import { Alert, Button, Card, Form, Skeleton } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { MEAL_TEXT } from '../constants';
import { useMealSettings, useSaveMealSettings } from '../hooks/useMeal';
import { PageCard } from '@/components/common/PageCard';
import { AppTimePicker } from '@/components/common/DateInputs';

type FormValues = { reminder: Dayjs; cutoff: Dayjs };

const FORMAT = 'HH:mm';
/** Khung giờ chốt cho phép (khớp BE MealSettingsService). */
const EARLIEST = '05:00';
const LATEST = '11:00';
const text = MEAL_TEXT.settings;

/** Admin cấu hình giờ nhắc tổ trưởng xác nhận và giờ tự chốt suất ăn; mặc định 08:15 và 08:30. */
export function MealSettingsPage() {
  const [form] = Form.useForm<FormValues>();
  const settings = useMealSettings();
  const save = useSaveMealSettings();
  const data = settings.data;

  useEffect(() => {
    if (data)
      form.setFieldsValue({
        reminder: dayjs(data.reminderTime, FORMAT),
        cutoff: dayjs(data.cutoffTime, FORMAT),
      });
  }, [data, form]);

  return (
    <PageCard
      title={MEAL_TEXT.settingsPageTitle}
      description={text.subtitle}
      style={{ maxWidth: 560 }}
    >
      <QueryError error={settings.error} onRetry={() => void settings.refetch()} />
      {settings.isLoading && <Skeleton active />}
      {data && (
        <Card>
          <Alert type="info" showIcon message={text.hint} style={{ marginBottom: 16 }} />
          <Form
            form={form}
            layout="vertical"
            disabled={save.isPending}
            onFinish={(v) =>
              save.mutate({
                reminderTime: v.reminder.format(FORMAT),
                cutoffTime: v.cutoff.format(FORMAT),
              })
            }
          >
            <Form.Item
              name="reminder"
              label={text.reminder}
              extra={text.reminderHelp}
              rules={[{ required: true, message: text.required }]}
            >
              <AppTimePicker minuteStep={5} allowClear={false} needConfirm={false} />
            </Form.Item>
            <Form.Item
              name="cutoff"
              label={text.cutoff}
              extra={text.cutoffHelp.replace('{from}', EARLIEST).replace('{to}', LATEST)}
              dependencies={['reminder']}
              rules={[
                { required: true, message: text.required },
                ({ getFieldValue }) => ({
                  validator: (_, value?: Dayjs) => {
                    const reminder = getFieldValue('reminder') as Dayjs | undefined;
                    if (!value) return Promise.resolve();
                    const hm = value.format(FORMAT);
                    if (hm < EARLIEST || hm > LATEST)
                      return Promise.reject(
                        new Error(
                          text.cutoffRange.replace('{from}', EARLIEST).replace('{to}', LATEST),
                        ),
                      );
                    if (reminder && reminder.format(FORMAT) >= hm)
                      return Promise.reject(new Error(text.order));
                    return Promise.resolve();
                  },
                }),
              ]}
            >
              <AppTimePicker minuteStep={5} allowClear={false} needConfirm={false} />
            </Form.Item>
            <Button type="primary" htmlType="submit" loading={save.isPending}>
              {text.save}
            </Button>
          </Form>
        </Card>
      )}
    </PageCard>
  );
}
