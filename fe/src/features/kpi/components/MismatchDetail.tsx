import { useMemo, useState } from 'react';
import { Alert, Input, Tag, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import type { EndOfOrderLine, EndOfOrderOperation, EndOfOrderWorker } from '../types';
import { operationMatches } from '../utils/mismatch';

const text = KPI_TEXT.finish;

const workerColumns: TableColumnsType<EndOfOrderWorker> = [
  { title: text.worker, key: 'worker', render: (_, w) => `${w.workerCode} - ${w.workerName}` },
  {
    title: text.reported,
    dataIndex: 'reportedQty',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
];

const lineColumns: TableColumnsType<EndOfOrderLine> = [
  { title: text.line, key: 'line', render: (_, l) => `${l.color} - ${l.sizeCode}` },
  {
    title: text.reported,
    dataIndex: 'reportedQty',
    align: 'right',
    render: (v: number) => formatQty(v),
  },
  { title: text.qcPassed, dataIndex: 'qcQty', align: 'right', render: (v: number) => formatQty(v) },
  { title: text.scrap, dataIndex: 'scrapQty', align: 'right', render: (v: number) => formatQty(v) },
  {
    title: text.mismatch,
    dataIndex: 'mismatchQty',
    align: 'right',
    render: (v: number) => <Tag color="red">{formatQty(v)}</Tag>,
  },
];

const operationColumns: TableColumnsType<EndOfOrderOperation> = [
  { title: text.operation, key: 'op', render: (_, o) => `${o.operationCode} - ${o.operationName}` },
  { title: text.mismatchLines, key: 'lines', align: 'right', render: (_, o) => o.lines.length },
  {
    title: text.mismatch,
    dataIndex: 'mismatchQty',
    align: 'right',
    render: (v: number) => <Tag color="red">{formatQty(v)}</Tag>,
  },
];

type MismatchDetailProps = { operations: EndOfOrderOperation[]; emptyText: string };

/**
 * Cảnh báo lệch kiểm cuối đơn theo ba tầng: công đoạn → dòng đơn → công nhân đã báo. Công đoạn, dòng đơn lệch nhiều nhất lên đầu, người
 * báo nhiều nhất lên đầu (BE đã sắp xếp). Có ô tìm theo công đoạn hoặc tên công nhân khi danh sách dài. Không có lệch thì hiện `emptyText`.
 */
export function MismatchDetail({ operations, emptyText }: MismatchDetailProps) {
  const [keyword, setKeyword] = useState('');
  const shown = useMemo(
    () => operations.filter((o) => operationMatches(o, keyword)),
    [operations, keyword],
  );

  if (operations.length === 0) return <Alert type="success" showIcon message={emptyText} />;

  return (
    <>
      <Alert
        type="warning"
        showIcon
        message={text.mismatchTitle.replace('{n}', String(operations.length))}
        style={{ marginBottom: 12 }}
      />
      <Input.Search
        allowClear
        placeholder={text.search}
        value={keyword}
        onChange={(e) => setKeyword(e.target.value)}
        style={{ marginBottom: 12, maxWidth: 320 }}
      />
      <DataTable<EndOfOrderOperation>
        rowKey="operationId"
        size="small"
        pagination={false}
        columns={operationColumns}
        dataSource={shown}
        expandable={{
          defaultExpandAllRows: true,
          expandedRowRender: (o) => (
            <DataTable<EndOfOrderLine>
              rowKey="orderLineId"
              size="small"
              pagination={false}
              columns={lineColumns}
              dataSource={o.lines}
              expandable={{
                expandedRowRender: (l) => (
                  <DataTable<EndOfOrderWorker>
                    rowKey="workerId"
                    size="small"
                    pagination={false}
                    title={() => text.workersTitle}
                    columns={workerColumns}
                    dataSource={l.workers}
                  />
                ),
              }}
            />
          ),
        }}
      />
    </>
  );
}
