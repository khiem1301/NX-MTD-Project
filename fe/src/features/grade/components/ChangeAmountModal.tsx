import { Alert, Form, Modal, Tag, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { API_DATE, formatCurrency, formatDate } from '@/utils/format';
import { GR_LABELS } from '../constants';
import { useAddLevel, useUpdateLevel } from '../hooks/useGrade';
import type { GradeCode, GradeLevel } from '../types';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { REQUIRED_RULES } from '../constants';
import { levelColumns } from './levelColumns';
import { MoneyInput } from './MoneyInput';
import { currentLevel, levelsAround } from '../utils';

/** Đổi tiền một bậc: đặt mức mới từ một ngày hiệu lực, kể cả ngày trong tương lai; chọn trùng ngày một mức đã hẹn thì thay mức đó. */
export function ChangeAmountModal({
  code,
  rows,
  onClose,
}: {
  code: GradeCode | null;
  rows: GradeLevel[];
  onClose: () => void;
}) {
  const [form] = Form.useForm<{ monthlyAmount: number; effectiveDate: Dayjs }>();
  const add = useAddLevel();
  const update = useUpdateLevel();
  const levels = rows.filter((l) => l.gradeCode === code?.gradeCode);
  const current = code ? currentLevel(rows, code.gradeCode) : undefined;
  // Chỉ cần sau ngày hiệu lực của mức đang áp dụng; các mức đã hẹn phía sau vẫn đặt thêm hoặc thay được.
  const earliest = current ? dayjs(current.effectiveDate).add(1, 'day') : null;
  const defaultDate = earliest && earliest.isAfter(dayjs(), 'day') ? earliest : dayjs();
  const amount = Form.useWatch('monthlyAmount', form);
  const date = Form.useWatch('effectiveDate', form);
  const around = levelsAround(levels, (date ?? defaultDate).format(API_DATE));
  const base = around.before;
  const diff = base && amount !== null && amount !== undefined ? amount - base.monthlyAmount : null;
  return (
    <Modal
      open={code !== null}
      title={
        code
          ? `${current ? GR_LABELS.changeAmount : GR_LABELS.addLevel} - ${code.gradeCode} · ${code.gradeName}`
          : ''
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH.md}
      confirmLoading={add.isPending || update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {code && (
        <Form
          form={form}
          layout="vertical"
          disabled={add.isPending || update.isPending}
          initialValues={{ monthlyAmount: current?.monthlyAmount, effectiveDate: defaultDate }}
          onFinish={(v) => {
            const effectiveDate = v.effectiveDate.format(API_DATE);
            const replaced = levelsAround(levels, effectiveDate).sameDay;
            if (replaced) {
              update.mutate(
                {
                  id: replaced.gradeLevelId,
                  body: { monthlyAmount: v.monthlyAmount, effectiveDate },
                },
                { onSuccess: onClose },
              );
              return;
            }
            add.mutate(
              { gradeCode: code.gradeCode, monthlyAmount: v.monthlyAmount, effectiveDate },
              { onSuccess: onClose },
            );
          }}
        >
          {current && (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message={`${GR_LABELS.currentAmount}: ${formatCurrency(current.monthlyAmount)} (${GR_LABELS.effectiveDate.toLowerCase()} ${formatDate(current.effectiveDate)})`}
            />
          )}
          <Typography.Text strong>{GR_LABELS.levelHistory}</Typography.Text>
          <DataTable
            rowKey="gradeLevelId"
            columns={levelColumns(levels)}
            dataSource={levels}
            pagination={false}
            scroll={{ x: 'max-content', y: 180 }}
            style={{ margin: '8px 0 16px' }}
          />
          <Form.Item
            name="monthlyAmount"
            label={GR_LABELS.newAmount}
            dependencies={['effectiveDate']}
            rules={[
              ...REQUIRED_RULES,
              ({ getFieldValue }) => ({
                validator: async (_rule, value?: number) => {
                  const d = getFieldValue('effectiveDate') as Dayjs | undefined;
                  const ref = d ? levelsAround(levels, d.format(API_DATE)).inEffect : current;
                  if (ref && value === ref.monthlyAmount) throw new Error(GR_LABELS.sameAmount);
                },
              }),
            ]}
          >
            <MoneyInput />
          </Form.Item>
          {diff !== null && diff !== 0 && (
            <div style={{ marginTop: -16, marginBottom: 16 }}>
              <Tag color={diff > 0 ? 'green' : 'red'}>
                {`${diff > 0 ? '+' : '-'}${formatCurrency(Math.abs(diff))}${
                  base && base.monthlyAmount > 0
                    ? ` (${diff > 0 ? '+' : '-'}${Math.round((Math.abs(diff) / base.monthlyAmount) * 1000) / 10}%)`
                    : ''
                }`}
              </Tag>
            </div>
          )}
          <Form.Item
            name="effectiveDate"
            label={GR_LABELS.effectiveDate}
            rules={REQUIRED_RULES}
            extra={
              around.sameDay
                ? `${GR_LABELS.replaceHint}: ${formatCurrency(around.sameDay.monthlyAmount)}`
                : GR_LABELS.effectiveHint
            }
          >
            <AppDatePicker
              allowClear={false}
              disabledDate={(d) => earliest !== null && d.isBefore(earliest, 'day')}
            />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
