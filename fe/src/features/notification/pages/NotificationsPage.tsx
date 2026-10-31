import { useState } from 'react';
import { Button, Card, Tabs } from 'antd';
import { Can } from '@/components/guards/Can';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { InboxTab } from '../components/InboxTab';
import { SendModal } from '../components/SendModal';
import { SentTab } from '../components/SentTab';

/** UC05 / UC20: hộp thư thông báo của mình; Admin / Quản đốc gửi thông báo nhanh và xem thông báo đã gửi. */
export function NotificationsPage() {
  const [sendOpen, setSendOpen] = useState(false);
  const { hasRole } = usePermission();
  const isSender = hasRole(ROLES.Admin, ROLES.Manager);
  return (
    <Card
      title="Thông báo"
      extra={
        <Can roles={[ROLES.Admin, ROLES.Manager]}>
          <Button type="primary" onClick={() => setSendOpen(true)}>
            Gửi thông báo nhanh
          </Button>
        </Can>
      }
    >
      <Tabs
        items={[
          { key: 'inbox', label: 'Hộp thư', children: <InboxTab /> },
          ...(isSender ? [{ key: 'sent', label: 'Đã gửi', children: <SentTab /> }] : []),
        ]}
      />
      <SendModal open={sendOpen} onClose={() => setSendOpen(false)} />
    </Card>
  );
}
