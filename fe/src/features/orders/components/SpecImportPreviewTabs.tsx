import { Alert, Space, Tabs, Tag, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { SpecImportPreview } from '../types';
import {
  componentImportColumns,
  groupImportColumns,
  markerImportColumns,
  normImportColumns,
} from './specImportColumns';

const text = MESSAGES.orders.specImport;
const TABLE_SCROLL = { x: 'max-content', y: 360 } as const;

type SpecImportPreviewTabsProps = { data: SpecImportPreview };

/** Kết quả xem trước: tóm tắt (MSG24), lỗi của bản đã gộp, ghi chú, và một tab cho mỗi bảng có trong file. */
export function SpecImportPreviewTabs({ data }: SpecImportPreviewTabsProps) {
  const tabs = [
    data.hasComponents && {
      key: 'components',
      label: fill(text.tabComponents, data.components.length),
      children: (
        <DataTable
          rowKey="rowNumber"
          size="small"
          columns={componentImportColumns}
          dataSource={data.components}
          scroll={TABLE_SCROLL}
        />
      ),
    },
    data.hasNorms && {
      key: 'norms',
      label: fill(text.tabNorms, data.norms.length),
      children: (
        <DataTable
          rowKey="rowNumber"
          size="small"
          columns={normImportColumns}
          dataSource={data.norms}
          scroll={TABLE_SCROLL}
        />
      ),
    },
    data.hasMarkerGroups && {
      key: 'groups',
      label: fill(text.tabGroups, data.markerGroups.length),
      children: (
        <DataTable
          rowKey="rowNumber"
          size="small"
          columns={groupImportColumns}
          dataSource={data.markerGroups}
          scroll={TABLE_SCROLL}
        />
      ),
    },
    data.hasOrderMarkers && {
      key: 'markers',
      label: fill(text.tabMarkers, data.orderMarkers.length),
      children: (
        <DataTable
          rowKey="rowNumber"
          size="small"
          columns={markerImportColumns(data.orderSizes ?? [])}
          dataSource={data.orderMarkers}
          scroll={TABLE_SCROLL}
        />
      ),
    },
  ].filter((t) => t !== false);

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Text>
          {fill(text.summary, data.validCount, data.errorCount, data.duplicateCount)}
        </Typography.Text>
        <Tag color={data.changeCount > 0 ? 'blue' : 'default'}>
          {fill(text.changes, data.changeCount)}
        </Tag>
      </Space>
      {data.crossErrors.length > 0 && (
        <Alert
          type="error"
          showIcon
          message={text.crossErrors}
          description={
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {data.crossErrors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          }
        />
      )}
      {data.notes.map((note) => (
        <Alert key={note} type="info" showIcon message={note} />
      ))}
      <Tabs items={tabs} />
    </Space>
  );
}
