import { DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { VL_LABELS } from '../constants';
import { useCompleteTrip, useStartTrip, useVehicles } from '../hooks/useVehicleLog';
import type { VehicleTrip } from '../types';

const API_DATE = 'YYYY-MM-DD';
const required = [{ required: true, message: MESSAGES.common.required }];

type StartForm = {
  vehicleId: number;
  tripDate: Dayjs;
  driverName: string;
  purpose?: string;
  odometerStart: number;
};

/** UC35: ghi chuyến mới. Km đầu không liền với km cuối chuyến trước vẫn lưu được nhưng bị cảnh báo (MSG30). */
export function TripStartModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<StartForm>();
  const vehicles = useVehicles({ status: 'ACTIVE', page: 1, pageSize: MAX_PAGE_SIZE });
  const start = useStartTrip();

  return (
    <Modal
      open={open}
      title={VL_LABELS.startTrip}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={start.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={start.isPending}
        initialValues={{ tripDate: dayjs() }}
        onFinish={(v) =>
          start.mutate(
            {
              vehicleId: v.vehicleId,
              tripDate: v.tripDate.format(API_DATE),
              driverName: v.driverName.trim(),
              purpose: v.purpose?.trim() || undefined,
              odometerStart: v.odometerStart,
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item name="vehicleId" label={VL_LABELS.vehicle} rules={required}>
          <Select
            placeholder={VL_LABELS.selectVehicle}
            loading={vehicles.isLoading}
            onChange={(id: number) => {
              const last = vehicles.data?.items.find((x) => x.vehicleId === id)?.lastOdometer;
              // Gợi ý km đầu = km hiện tại của xe để chuyến liền nhau, người dùng vẫn sửa được.
              if (last !== null && last !== undefined) form.setFieldValue('odometerStart', last);
            }}
            options={(vehicles.data?.items ?? []).map((x) => ({
              value: x.vehicleId,
              label: `${x.plateNumber} · ${x.vehicleType}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="tripDate" label={VL_LABELS.date} rules={required}>
          <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item
          name="driverName"
          label={VL_LABELS.driver}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input maxLength={100} />
        </Form.Item>
        <Form.Item name="purpose" label={VL_LABELS.purpose}>
          <Input maxLength={300} />
        </Form.Item>
        <Form.Item name="odometerStart" label={VL_LABELS.odometerStart} rules={required}>
          <InputNumber min={0} precision={0} style={{ width: 200 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}

type CompleteForm = { odometerEnd: number; fuelLiters?: number | null; fuelCost?: number | null };

/** UC35: kết thúc chuyến (km cuối, xăng, tiền xăng). Xăng vượt định mức × (1 + dung sai) bị cảnh báo (MSG30). */
export function TripCompleteModal({
  trip,
  onClose,
}: {
  trip: VehicleTrip | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<CompleteForm>();
  const complete = useCompleteTrip();

  return (
    <Modal
      open={trip !== null}
      title={`${VL_LABELS.completeTrip}${trip ? ` — ${trip.plateNumber}` : ''}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={complete.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {trip && (
        <Form
          form={form}
          layout="vertical"
          disabled={complete.isPending}
          onFinish={(v) =>
            complete.mutate({ id: trip.vehicleTripId, body: v }, { onSuccess: onClose })
          }
        >
          <Form.Item label={VL_LABELS.odometerStart}>
            <InputNumber value={trip.odometerStart} disabled style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="odometerEnd" label={VL_LABELS.odometerEnd} rules={required}>
            <InputNumber min={trip.odometerStart + 1} precision={0} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="fuelLiters" label={VL_LABELS.fuelLiters}>
            <InputNumber min={0} step={0.5} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item name="fuelCost" label={VL_LABELS.fuelCost}>
            <InputNumber min={0} step={1000} style={{ width: 200 }} />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
