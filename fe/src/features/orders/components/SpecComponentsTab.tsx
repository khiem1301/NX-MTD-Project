import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, Input, InputNumber, Select, Space, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { newKey, patchRow, type ComponentRow } from '../utils/productSpec';
import { deleteRowColumn } from './deleteRowColumn';

const text = MESSAGES.orders.spec;

const TYPE_OPTIONS = [
  { value: 'Panel', label: text.typePanel },
  { value: 'Trim', label: text.typeTrim },
];

type SpecComponentsTabProps = {
  components: ComponentRow[];
  setComponents: Dispatch<SetStateAction<ComponentRow[]>>;
  /** Tên các thành phần đã ngừng dùng (đã có số liệu sản xuất), chỉ để báo. */
  discontinued: string[];
};

/** Tab "Thành phần" của định mức: sản phẩm gồm những chi tiết / phụ liệu nào, khai một lần cho mọi màu – size. */
export function SpecComponentsTab({
  components,
  setComponents,
  discontinued,
}: SpecComponentsTabProps) {
  return (
    <>
      <Typography.Paragraph type="secondary">{text.componentHint}</Typography.Paragraph>
      <DataTable<ComponentRow>
        rowKey="key"
        size="small"
        pagination={false}
        dataSource={components}
        columns={[
          {
            title: text.componentName,
            render: (_, r) => (
              <Input
                size="small"
                value={r.componentName}
                maxLength={100}
                onChange={(e) => patchRow(setComponents, r.key, { componentName: e.target.value })}
              />
            ),
          },
          {
            title: text.componentType,
            width: 190,
            render: (_, r) => (
              <Select
                size="small"
                value={r.componentType}
                options={TYPE_OPTIONS}
                style={{ width: 170 }}
                onChange={(v) => patchRow(setComponents, r.key, { componentType: v })}
              />
            ),
          },
          {
            title: text.pieces,
            width: 140,
            render: (_, r) => (
              <Space direction="vertical" size={2}>
                <InputNumber
                  size="small"
                  min={1}
                  max={99}
                  precision={0}
                  value={r.piecesPerGarment}
                  onChange={(v) => patchRow(setComponents, r.key, { piecesPerGarment: v ?? 1 })}
                />
                {/tay/i.test(r.componentName) && r.piecesPerGarment === 2 && (
                  <span style={{ fontSize: 12, opacity: 0.65 }}>Gồm tay trái + tay phải</span>
                )}
              </Space>
            ),
          },
          deleteRowColumn(setComponents),
        ]}
      />
      <Button
        type="dashed"
        icon={<PlusOutlined />}
        style={{ marginTop: 8 }}
        onClick={() =>
          setComponents((rows) => [
            ...rows,
            {
              key: newKey(),
              productComponentId: null,
              componentName: '',
              componentType: 'Panel',
              piecesPerGarment: 1,
            },
          ])
        }
      >
        {text.addComponent}
      </Button>
      {discontinued.length > 0 && (
        <Alert
          style={{ marginTop: 12 }}
          type="info"
          showIcon
          message={`${text.discontinued}: ${discontinued.join(', ')}`}
        />
      )}
    </>
  );
}
