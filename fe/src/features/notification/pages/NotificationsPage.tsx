import { useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Input,
  Modal,
  Radio,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  type TableColumnsType,
} from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import {
  useAnnouncementTargets,
  useMarkAllRead,
  useMarkRead,
  useNotifications,
  useSendAnnouncement,
  useSentAnnouncements,
} from '../hooks/useNotifications';
import type { Announcement, NotificationItem } from '../types';

const SCOPE_LABEL: Record<string, string> = {
  ALL: 'Toàn xưởng',
  TEAM: 'Theo tổ',
  ROLE: 'Theo vai trò',
};
const SCOPE_OPTIONS = Object.entries(SCOPE_LABEL).map(([value, label]) => ({ value, label }));
const PRIORITY_OPTIONS = [
  { value: 'NORMAL', label: 'Thường' },
  { value: 'URGENT', label: 'Khẩn' },
];

function InboxTab() {
  const table = useTableQuery<{ unreadOnly?: boolean }>({});
  const list = useNotifications(table.params);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  const columns: TableColumnsType<NotificationItem> = [
    {
      title: 'Thông báo',
      key: 'title',
      render: (_, n) => (
        <Space direction="vertical" size={0}>
          <Space size={4}>
            {n.isUrgent && <Tag color="red">Khẩn</Tag>}
            <strong style={{ fontWeight: n.isRead ? 400 : 600 }}>{n.title}</strong>
          </Space>
          <span>{n.message}</span>
        </Space>
      ),
    },
    { title: 'Lúc', dataIndex: 'createdAt', render: formatDateTime },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, n) =>
        n.isRead ? null : (
          <Button type="link" onClick={() => markRead.mutate(n.notificationId)}>
            Đánh dấu đã đọc
          </Button>
        ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Checkbox onChange={(e) => table.setFilters({ unreadOnly: e.target.checked || undefined })}>
          Chỉ chưa đọc
        </Checkbox>
        <Button onClick={() => markAll.mutate(undefined)}>Đánh dấu tất cả đã đọc</Button>
      </Space>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <Table
        rowKey="notificationId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
    </>
  );
}

type SendForm = {
  content: string;
  priority: string;
  targetScope: string;
  teamIds?: number[];
  roleIds?: number[];
};

function SendModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<SendForm>();
  const targets = useAnnouncementTargets(open);
  const send = useSendAnnouncement();
  const scope = Form.useWatch('targetScope', form) ?? 'ALL';

  return (
    <Modal
      open={open}
      title="Gửi thông báo nhanh"
      okText="Gửi"
      cancelText={MESSAGES.common.cancel}
      confirmLoading={send.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={send.isPending}
        initialValues={{ priority: 'NORMAL', targetScope: 'ALL' }}
        onFinish={(v) => send.mutate({ ...v, content: v.content.trim() }, { onSuccess: onClose })}
      >
        <Form.Item
          name="content"
          label="Nội dung"
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input.TextArea rows={4} maxLength={1000} showCount />
        </Form.Item>
        <Form.Item name="priority" label="Mức ưu tiên">
          <Radio.Group optionType="button" options={PRIORITY_OPTIONS} />
        </Form.Item>
        <Form.Item name="targetScope" label="Gửi tới">
          <Radio.Group optionType="button" options={SCOPE_OPTIONS} />
        </Form.Item>
        {scope === 'TEAM' && (
          <Form.Item
            name="teamIds"
            label="Tổ"
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              mode="multiple"
              loading={targets.isLoading}
              options={(targets.data?.teams ?? []).map((t) => ({
                value: t.teamId,
                label: t.teamName,
              }))}
            />
          </Form.Item>
        )}
        {scope === 'ROLE' && (
          <Form.Item
            name="roleIds"
            label="Vai trò"
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <Select
              mode="multiple"
              loading={targets.isLoading}
              options={(targets.data?.roles ?? []).map((r) => ({
                value: r.roleId,
                label: r.roleName,
              }))}
            />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}

function SentTab() {
  const table = useTableQuery<Record<string, never>>({});
  const list = useSentAnnouncements(table.params);
  const columns: TableColumnsType<Announcement> = [
    { title: 'Nội dung', dataIndex: 'content' },
    {
      title: 'Mức',
      dataIndex: 'priority',
      render: (v: string) => (v === 'URGENT' ? <Tag color="red">Khẩn</Tag> : 'Thường'),
    },
    {
      title: 'Gửi tới',
      key: 'scope',
      render: (_, a) =>
        [SCOPE_LABEL[a.targetScope], ...a.targets]
          .join(': ')
          .replace(': ', a.targets.length ? ': ' : ''),
    },
    { title: 'Người nhận', dataIndex: 'recipientCount', align: 'right' },
    { title: 'Người gửi', dataIndex: 'senderName' },
    { title: 'Lúc', dataIndex: 'sentAt', render: formatDateTime },
  ];
  return (
    <>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <Table
        rowKey="announcementId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
    </>
  );
}

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
