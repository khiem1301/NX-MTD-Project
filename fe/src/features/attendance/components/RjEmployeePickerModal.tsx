import { Modal, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ATTENDANCE_TEXT } from '../constants';
import { useRjEmployeesCommon } from '../hooks/useAttendance';
import type { RjEmployee } from '../types';

type Props = {
  open: boolean;
  onClose: () => void;
  onPick: (row: RjEmployee) => void;
};

/** Modal chọn NV từ snapshot RJ khi tạo/sửa nhân viên trên web. */
export function RjEmployeePickerModal({ open, onClose, onPick }: Props) {
  const { data, isLoading } = useRjEmployeesCommon(true, open);

  const columns: ColumnsType<RjEmployee> = [
    {
      title: ATTENDANCE_TEXT.table.attendanceCode,
      dataIndex: 'attendanceCode',
      width: 120,
      render: (v: number) => <Tag color="blue">{v}</Tag>,
    },
    {
      title: ATTENDANCE_TEXT.rj.rjCode,
      dataIndex: 'rjEmployeeCode',
      width: 120,
      render: (v: string | null) => v ?? '—',
    },
    {
      title: ATTENDANCE_TEXT.table.employee,
      dataIndex: 'fullName',
    },
  ];

  return (
    <Modal
      open={open}
      title={ATTENDANCE_TEXT.rj.pickTitle}
      onCancel={onClose}
      footer={null}
      width={720}
      destroyOnClose
    >
      <Table<RjEmployee>
        rowKey="attendanceCode"
        size="small"
        loading={isLoading}
        columns={columns}
        dataSource={data ?? []}
        pagination={{ pageSize: 10 }}
        onRow={(row) => ({
          onClick: () => {
            onPick(row);
            onClose();
          },
          style: { cursor: 'pointer' },
        })}
        locale={{ emptyText: ATTENDANCE_TEXT.rj.empty }}
      />
    </Modal>
  );
}
