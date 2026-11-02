import { Button, Space } from 'antd';
import { ApartmentOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.operations;

type OperationPageActionsProps = {
  disabled: boolean;
  onManageGroups: () => void;
  onImport: () => void;
  onAdd: () => void;
};

/** Các nút đầu màn công đoạn, chỉ dùng được khi đã chọn mã hàng. */
export function OperationPageActions({
  disabled,
  onManageGroups,
  onImport,
  onAdd,
}: OperationPageActionsProps) {
  return (
    <Space wrap>
      <Button icon={<ApartmentOutlined />} disabled={disabled} onClick={onManageGroups}>
        {text.manageGroups}
      </Button>
      <Button icon={<UploadOutlined />} disabled={disabled} onClick={onImport}>
        {text.importExcel}
      </Button>
      <Button type="primary" icon={<PlusOutlined />} disabled={disabled} onClick={onAdd}>
        {text.add}
      </Button>
    </Space>
  );
}
