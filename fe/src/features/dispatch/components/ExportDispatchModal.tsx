import { QtyInput } from '@/components/common/QtyInput';
import { FORM_HINT_BELOW_CLASS } from '@/components/common/formHint';
import { Alert, Form, Input, Modal, Radio, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';
import {
  DELIVERY_METHOD,
  DELIVERY_METHOD_LABEL,
  DISPATCH_LABELS,
  DRIVER_NAME_MAX,
  MAX_ODOMETER,
} from '../constants';
import { useAvailableVehicles, useExportDispatch } from '../hooks/useDispatch';
import type { DeliveryMethod, DispatchNote } from '../types';

type Props = { note: DispatchNote; onClose: () => void; onDone: () => void };

type FormValues = {
  deliveryMethod: DeliveryMethod;
  vehicleId?: number;
  driverName?: string;
  odometerStart?: number | null;
};

const METHOD_OPTIONS = Object.values(DELIVERY_METHOD).map((value) => ({
  value,
  label: DELIVERY_METHOD_LABEL[value],
}));

/**
 * DEV-ALL-127: Thủ kho xuất hàng và chọn cách giao. Xe công ty giao thì chọn xe đang rảnh, tài xế, km lúc đi (km cuối
 * chuyến trước hiện thành gợi ý dưới ô để đối chiếu, Thủ kho tự nhập số trên đồng hồ); BE tạo luôn chuyến xe đang chạy gắn phiếu. Trang chỉ render khi mở.
 */
export function ExportDispatchModal({ note, onClose, onDone }: Props) {
  const [form] = Form.useForm<FormValues>();
  const exportGoods = useExportDispatch();
  const method = Form.useWatch('deliveryMethod', form);
  const byVehicle = method === DELIVERY_METHOD.COMPANY_VEHICLE;
  const vehicles = useAvailableVehicles(byVehicle);
  const vehicleId = Form.useWatch('vehicleId', form);
  const lastOdometer = vehicles.data?.find((v) => v.vehicleId === vehicleId)?.lastOdometer;

  const handleFinish = (values: FormValues) => {
    const vehicle = values.deliveryMethod === DELIVERY_METHOD.COMPANY_VEHICLE;
    exportGoods.mutate(
      {
        id: note.dispatchNoteId,
        body: {
          rowVer: note.rowVer,
          deliveryMethod: values.deliveryMethod,
          vehicleId: vehicle ? (values.vehicleId ?? null) : null,
          driverName: vehicle ? (values.driverName?.trim() ?? null) : null,
          odometerStart: vehicle ? (values.odometerStart ?? null) : null,
        },
      },
      { onSuccess: onDone },
    );
  };

  // Đổi xe thì bỏ số đã gõ: số đó là của đồng hồ xe trước.
  const clearOdometer = () => form.setFieldValue('odometerStart', null);

  return (
    <Modal
      open
      title={fill(DISPATCH_LABELS.exportTitle, note.dispatchCode)}
      okText={DISPATCH_LABELS.export}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={exportGoods.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        message={DISPATCH_LABELS.exportHint}
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        layout="vertical"
        disabled={exportGoods.isPending}
        initialValues={{ deliveryMethod: DELIVERY_METHOD.CUSTOMER_PICKUP }}
        onFinish={handleFinish}
      >
        <Form.Item name="deliveryMethod" label={DISPATCH_LABELS.deliveryMethod}>
          <Radio.Group options={METHOD_OPTIONS} optionType="button" buttonStyle="solid" />
        </Form.Item>
        {byVehicle && (
          <>
            <Form.Item
              name="vehicleId"
              label={DISPATCH_LABELS.vehicle}
              rules={[{ required: true, message: DISPATCH_LABELS.vehicleRequired }]}
              extra={vehicles.data?.length === 0 ? DISPATCH_LABELS.noVehicle : undefined}
            >
              <Select
                placeholder={DISPATCH_LABELS.vehiclePlaceholder}
                loading={vehicles.isFetching}
                onChange={clearOdometer}
                options={vehicles.data?.map((v) => ({
                  value: v.vehicleId,
                  label: `${v.plateNumber} · ${v.vehicleType}`,
                }))}
              />
            </Form.Item>
            <Form.Item
              name="driverName"
              label={DISPATCH_LABELS.driver}
              rules={[
                { required: true, whitespace: true, message: DISPATCH_LABELS.driverRequired },
              ]}
            >
              <Input maxLength={DRIVER_NAME_MAX} />
            </Form.Item>
            <Form.Item
              name="odometerStart"
              label={DISPATCH_LABELS.odometerStart}
              rules={[{ required: true, message: DISPATCH_LABELS.odometerRequired }]}
              className={FORM_HINT_BELOW_CLASS}
              extra={
                lastOdometer === null || lastOdometer === undefined
                  ? undefined
                  : fill(DISPATCH_LABELS.odometerHint, formatQty(lastOdometer))
              }
            >
              <QtyInput min={0} max={MAX_ODOMETER} style={{ width: 200 }} />
            </Form.Item>
            <Alert type="warning" showIcon message={DISPATCH_LABELS.tripHint} />
          </>
        )}
      </Form>
    </Modal>
  );
}
