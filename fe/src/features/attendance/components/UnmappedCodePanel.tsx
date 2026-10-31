import { useState } from 'react';
import { Alert, Button, Empty, Modal, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { LinkOutlined } from '@ant-design/icons';
import { ATTENDANCE_TEXT } from '../constants';
import { useMapAttendanceCode, useUnmappedCodes } from '../hooks/useAttendance';
import type { UnmappedCode } from '../types';
import { formatDateTime } from '../utils';
import { MapCodeForm } from './MapCodeForm';
import { DataTable } from '@/components/common/DataTable';

/**
 * UC28 — mã chấm công có lượt quẹt nhưng chưa gắn nhân viên. Gán xong thì thủ tục
 * usp_Attendance_MapCode nhận lại lượt cũ và tính lại bảng công, nên không cần thao tác thêm.
 */
export function UnmappedCodePanel() {
  const { data, isLoading, error } = useUnmappedCodes();
  const mapCode = useMapAttendanceCode();
  const [selected, setSelected] = useState<UnmappedCode | null>(null);

  if (error) {
    return <Alert type="error" showIcon message={(error as { message: string }).message} />;
  }

  const columns: ColumnsType<UnmappedCode> = [
    {
      title: ATTENDANCE_TEXT.table.attendanceCode,
      dataIndex: 'attendanceCode',
      width: 150,
      render: (value: number) => <Tag color="blue">{value}</Tag>,
    },
    {
      title: ATTENDANCE_TEXT.table.punchCount,
      dataIndex: 'punchCount',
      width: 120,
      align: 'right',
    },
    {
      title: ATTENDANCE_TEXT.table.firstPunch,
      dataIndex: 'firstPunchAt',
      responsive: ['md'],
      render: (value: string) => formatDateTime(value),
    },
    {
      title: ATTENDANCE_TEXT.table.lastPunch,
      dataIndex: 'lastPunchAt',
      render: (value: string) => formatDateTime(value),
    },
    {
      title: ATTENDANCE_TEXT.table.action,
      key: 'action',
      width: 130,
      fixed: 'right',
      render: (_, row) => (
        <Button
          type="primary"
          size="small"
          icon={<LinkOutlined />}
          onClick={() => setSelected(row)}
        >
          {ATTENDANCE_TEXT.map.submit}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {!isLoading && (data?.length ?? 0) > 0 ? (
        <Alert
          type="info"
          showIcon
          message={
            <span>
              Có <Typography.Text strong>{data?.length}</Typography.Text> mã chấm công chưa gắn nhân
              viên. Lượt quẹt vẫn được lưu, gán xong sẽ tự tính lại ngày công.
            </span>
          }
        />
      ) : null}

      <DataTable<UnmappedCode>
        rowKey="attendanceCode"
        size="small"
        columns={columns}
        dataSource={data ?? []}
        loading={isLoading}
        pagination={false}
        scroll={{ x: 'max-content' }}
        locale={{
          emptyText: (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={
                <div>
                  <div>{ATTENDANCE_TEXT.map.emptyTitle}</div>
                  <Typography.Text type="secondary">
                    {ATTENDANCE_TEXT.map.emptyHint}
                  </Typography.Text>
                </div>
              }
            />
          ),
        }}
      />

      <Modal
        open={selected !== null}
        title={ATTENDANCE_TEXT.map.title}
        onCancel={() => setSelected(null)}
        footer={null}
        destroyOnClose
      >
        {selected ? (
          <MapCodeForm
            code={selected}
            submitting={mapCode.isPending}
            onCancel={() => setSelected(null)}
            onSubmit={(employeeId) =>
              mapCode.mutate(
                { attendanceCode: selected.attendanceCode, employeeId },
                { onSuccess: () => setSelected(null) },
              )
            }
          />
        ) : null}
      </Modal>
    </Space>
  );
}
