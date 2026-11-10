import { Modal, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { useRjEmployeesCommon } from '@/hooks/useRjEmployees';
import type { RjEmployee } from '@/types/rjEmployee';

type RjEmployeePickerModalProps = {
  open: boolean;
  onClose: () => void;
  onPick: (row: RjEmployee) => void;
};

const PAGE_SIZE = 10;
const MODAL_WIDTH_PX = 720;

/** Modal chọn NV từ snapshot RJ khi tạo/sửa nhân viên trên web. */
export function RjEmployeePickerModal({ open, onClose, onPick }: RjEmployeePickerModalProps) {
  const { data, isLoading } = useRjEmployeesCommon(true, open);
  const text = MESSAGES.hr.rj;

  const columns: ColumnsType<RjEmployee> = [
    {
      title: MESSAGES.hr.attendanceCode,
      dataIndex: 'attendanceCode',
      width: 120,
      render: (v: number) => <Tag color="blue">{v}</Tag>,
    },
    {
      title: text.code,
      dataIndex: 'rjEmployeeCode',
      width: 120,
      render: (v: string | null) => v ?? '—',
    },
    { title: text.employee, dataIndex: 'fullName' },
  ];

  return (
    <Modal
      open={open}
      title={text.pickTitle}
      onCancel={onClose}
      footer={null}
      width={MODAL_WIDTH_PX}
      destroyOnClose
    >
      <Table<RjEmployee>
        rowKey="attendanceCode"
        size="small"
        loading={isLoading}
        columns={columns}
        dataSource={data ?? []}
        pagination={{ pageSize: PAGE_SIZE }}
        onRow={(row) => ({
          onClick: () => {
            onPick(row);
            onClose();
          },
          style: { cursor: 'pointer' },
        })}
        locale={{ emptyText: text.empty }}
      />
    </Modal>
  );
}
