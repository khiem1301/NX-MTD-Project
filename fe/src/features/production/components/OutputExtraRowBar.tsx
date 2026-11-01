import { Button, Select, Space, Typography } from 'antd';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.output;

type Option = { value: number; label: string };

type OutputExtraRowBarProps = {
  operationOptions: Option[];
  workerOptions: Option[];
  operationsLoading: boolean;
  workersLoading: boolean;
  operationId: number | undefined;
  workerId: number | undefined;
  onOperationChange: (id: number | undefined) => void;
  onWorkerChange: (id: number | undefined) => void;
  onAdd: () => void;
};

/** Thêm công đoạn ngoài phân công cho riêng ca này (GBR-20): chọn công đoạn + công nhân rồi thêm dòng. */
export function OutputExtraRowBar({
  operationOptions,
  workerOptions,
  operationsLoading,
  workersLoading,
  operationId,
  workerId,
  onOperationChange,
  onWorkerChange,
  onAdd,
}: OutputExtraRowBarProps) {
  return (
    <Space wrap align="center">
      <Typography.Text type="secondary">{text.addOutside}:</Typography.Text>
      <Select<number>
        showSearch
        optionFilterProp="label"
        style={{ width: 280 }}
        placeholder={PRODUCTION_TEXT.selectOperation}
        loading={operationsLoading}
        value={operationId}
        options={operationOptions}
        onChange={onOperationChange}
      />
      <Select<number>
        showSearch
        optionFilterProp="label"
        style={{ width: 240 }}
        placeholder={PRODUCTION_TEXT.selectWorker}
        loading={workersLoading}
        value={workerId}
        options={workerOptions}
        onChange={onWorkerChange}
      />
      <Button disabled={operationId === undefined || workerId === undefined} onClick={onAdd}>
        {text.addRow}
      </Button>
    </Space>
  );
}
