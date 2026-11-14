import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Alert, Button, ConfigProvider, Input, Select, Typography } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
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
  /** Xóa một thành phần cùng các dòng định mức dùng nó (nằm ở cha vì định mức thuộc tab khác). */
  onRemove: (key: string) => void;
  /** Tên các thành phần đã ngừng dùng (đã có số liệu sản xuất), chỉ để báo. */
  discontinued: string[];
  /** Chế độ chỉ xem. Kế toán và Admin đều sửa được nên màn mã hàng không bật. */
  readOnly?: boolean;
};

/** Tab "Thành phần" của định mức: sản phẩm gồm những chi tiết / phụ liệu nào, khai một lần cho mọi màu – size. */
export function SpecComponentsTab({
  components,
  setComponents,
  onRemove,
  discontinued,
  readOnly = false,
}: SpecComponentsTabProps) {
  return (
    <>
      <Typography.Paragraph type="secondary">{text.componentHint}</Typography.Paragraph>
      <ConfigProvider componentDisabled={readOnly}>
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
                  onChange={(e) =>
                    patchRow(setComponents, r.key, { componentName: e.target.value })
                  }
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
                <QtyInput
                  size="small"
                  min={1}
                  max={99}
                  value={r.piecesPerGarment}
                  onChange={(v) => patchRow(setComponents, r.key, { piecesPerGarment: v ?? 1 })}
                />
              ),
            },
            ...(readOnly ? [] : [deleteRowColumn<ComponentRow>(onRemove)]),
          ]}
        />
      </ConfigProvider>
      {!readOnly && (
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
      )}
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
