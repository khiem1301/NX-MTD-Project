import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, Space } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { useTableQuery } from '@/hooks/useTableQuery';
import { RequestDetailModal } from '../components/RequestDetailModal';
import { RequestFilters, type RequestFilterValue } from '../components/RequestFilters';
import { RequestFormModal } from '../components/RequestFormModal';
import { RequestTable } from '../components/RequestTable';
import { DEFAULT_MAX_PHOTOS, GENERIC_FORM_TYPES } from '../constants';
import { useMyRequests, useRequestTypes } from '../hooks/useRequests';

/** UC07: đơn do mình tạo (tổ trưởng / tổ phó: đơn của cả tổ, D15). */
export function MyRequestsPage() {
  const { user } = useAuth();
  const [, setSearchParams] = useSearchParams();
  const table = useTableQuery<RequestFilterValue>({});
  const [creating, setCreating] = useState(false);
  const requests = useMyRequests(table.params);
  const { data: types } = useRequestTypes();
  const canCreate = (types ?? []).some((t) => GENERIC_FORM_TYPES.includes(t.requestType));
  const isTeamRole = user?.role === ROLES.TeamLeader || user?.role === ROLES.TeamDeputy;

  const open = (id: number) => setSearchParams({ id: String(id) });

  return (
    <Card
      title={MESSAGES.requests.myTitle}
      extra={
        canCreate && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>
            {MESSAGES.requests.create}
          </Button>
        )
      }
    >
      <Space direction="vertical" style={{ width: '100%' }}>
        <RequestFilters value={table.filters} onChange={table.setFilters} />
        <QueryError error={requests.error} onRetry={() => void requests.refetch()} />
        <RequestTable
          data={requests.data}
          loading={requests.isFetching}
          pagination={table.pagination(requests.data?.totalItems)}
          showTeam={isTeamRole}
          onOpen={open}
        />
      </Space>
      <RequestFormModal
        open={creating}
        maxPhotos={DEFAULT_MAX_PHOTOS}
        onClose={() => setCreating(false)}
        onSaved={(id) => {
          setCreating(false);
          open(id);
        }}
      />
      <RequestDetailModal />
    </Card>
  );
}
