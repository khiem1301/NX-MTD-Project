import { Button, Input, InputNumber, Select, Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined } from '@ant-design/icons';
import { ReasonHover } from '@/components/common/ReasonHover';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import type { OperationImportPreview } from '../types';
import type { OperationDraftRow } from '../utils/operationImportDraft';

const text = PRODUCTION_TEXT.operations.import;

type Ctx = {
  /** Kết quả kiểm gần nhất (hành động, lỗi, đơn giá, ghi chú đổi của từng dòng, theo rowNumber). */
  data: OperationImportPreview;
  update: (rowNumber: number, patch: Partial<OperationDraftRow>) => void;
  remove: (rowNumber: number) => void;
};

const textOf = (v: string | null | undefined) => v ?? '';
const orNull = (v: string) => (v.trim() === '' ? null : v);

/** DEV-ALL-179: cột sửa được của bảng công đoạn trên popup nhập Excel; kết quả kiểm lấy từ máy chủ theo rowNumber. */
export const operationImportEditColumns = ({
  data,
  update,
  remove,
}: Ctx): ColumnsType<OperationDraftRow> => {
  const statusOf = (rowNumber: number) => data.rows.find((r) => r.rowNumber === rowNumber);
  return [
    {
      title: text.columns.stt,
      key: 'stt',
      width: 80,
      render: (_, r) => (
        <InputNumber
          style={{ width: '100%' }}
          min={1}
          max={32767}
          precision={0}
          value={r.stt}
          onChange={(v) => update(r.rowNumber, { stt: v ?? 0 })}
        />
      ),
    },
    {
      title: text.columns.name,
      key: 'name',
      width: 220,
      render: (_, r) => (
        <Input
          value={textOf(r.name)}
          onChange={(e) => update(r.rowNumber, { name: e.target.value })}
        />
      ),
    },
    {
      title: text.columns.skill,
      key: 'skill',
      width: 80,
      render: (_, r) => (
        <InputNumber
          style={{ width: '100%' }}
          min={1}
          max={9}
          precision={0}
          value={r.skillLevel}
          onChange={(v) => update(r.rowNumber, { skillLevel: v })}
        />
      ),
    },
    {
      title: text.columns.time,
      key: 'time',
      width: 110,
      render: (_, r) => (
        <InputNumber
          style={{ width: '100%' }}
          min={0}
          precision={2}
          value={r.standardTimeSec}
          onChange={(v) => update(r.rowNumber, { standardTimeSec: v })}
        />
      ),
    },
    {
      title: text.columns.equipment,
      key: 'equipment',
      width: 140,
      render: (_, r) => (
        <Input
          value={textOf(r.equipment)}
          onChange={(e) => update(r.rowNumber, { equipment: orNull(e.target.value) })}
        />
      ),
    },
    ...(data.hasGroupColumn
      ? [
          {
            title: text.columns.group,
            key: 'group',
            width: 150,
            render: (_: unknown, r: OperationDraftRow) => (
              <Input
                value={textOf(r.groupName)}
                onChange={(e) => update(r.rowNumber, { groupName: orNull(e.target.value) })}
              />
            ),
          },
        ]
      : []),
    ...(data.hasComponentColumn
      ? [
          {
            title: text.columns.components,
            key: 'components',
            width: 220,
            render: (_: unknown, r: OperationDraftRow) => (
              <Select
                mode="tags"
                style={{ width: '100%' }}
                tokenSeparators={[';']}
                value={r.componentNames ?? []}
                open={false}
                onChange={(names: string[]) =>
                  update(r.rowNumber, { componentNames: names.length > 0 ? names : null })
                }
              />
            ),
          },
        ]
      : []),
    ...(data.hasRateColumn
      ? [
          {
            title: text.columns.rate,
            key: 'rate',
            width: 150,
            render: (_: unknown, r: OperationDraftRow) => {
              const status = statusOf(r.rowNumber);
              return (
                <Space direction="vertical" size={0} style={{ width: '100%' }}>
                  <InputNumber
                    style={{ width: '100%' }}
                    min={0}
                    precision={2}
                    value={r.rate}
                    onChange={(v) => update(r.rowNumber, { rate: v })}
                  />
                  {status && status.rateAction !== 'None' && (
                    <StatusTag status={status.rateAction} labels={text.rateAction} />
                  )}
                  {status?.rateNote && (
                    <Typography.Text type="secondary">{status.rateNote}</Typography.Text>
                  )}
                </Space>
              );
            },
          },
        ]
      : []),
    {
      title: text.columns.action,
      key: 'action',
      width: 150,
      fixed: 'right',
      render: (_, r) => {
        const status = statusOf(r.rowNumber);
        if (!status) return null;
        return (
          <ReasonHover
            reasons={[
              status.error && { text: status.error, type: 'danger' },
              status.warning && { text: status.warning, type: 'warning' },
              status.changeNote && { text: status.changeNote, type: 'secondary' },
            ]}
          >
            <StatusTag status={status.action} labels={text.action} />
          </ReasonHover>
        );
      },
    },
    {
      key: 'remove',
      width: 48,
      fixed: 'right',
      render: (_, r) => (
        <Button
          type="text"
          danger
          size="small"
          aria-label={MESSAGES.common.delete}
          icon={<DeleteOutlined />}
          onClick={() => remove(r.rowNumber)}
        />
      ),
    },
  ];
};
