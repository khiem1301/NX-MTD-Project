import { Card, Space } from 'antd';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { RequestDetailModal } from '../components/RequestDetailModal';
import { RequestFilters, type RequestFilterValue } from '../components/RequestFilters';
import { RequestTable } from '../components/RequestTable';
import { useRequestInbox } from '../hooks/useRequests';

/** UC08: đơn thuộc loại mình xử lý. Không chọn trạng thái = đang mở (BE mặc định). */
export function RequestInboxPage() {
  const [, setSearchParams] = useSearchParams();
  const table = useTableQuery<RequestFilterValue>({});
  const requests = useRequestInbox(table.params);

  return (
    <Card title={MESSAGES.requests.inboxTitle}>
      <Space direction="vertical" style={{ width: '100%' }}>
        <RequestFilters
          value={table.filters}
          onChange={table.setFilters}
          emptyStatusLabel={MESSAGES.requests.allOpen}
        />
        <QueryError error={requests.error} onRetry={() => void requests.refetch()} />
        <RequestTable
          data={requests.data}
          loading={requests.isFetching}
          pagination={table.pagination(requests.data?.totalItems)}
          showTeam
          onOpen={(id) => setSearchParams({ id: String(id) })}
        />
      </Space>
      <RequestDetailModal />
    </Card>
  );
}
