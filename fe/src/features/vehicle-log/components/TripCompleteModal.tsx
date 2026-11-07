import { Form, InputNumber, Modal } from 'antd';
import type { Rule } from 'antd/es/form';
import { MESSAGES } from '@/constants/messages';
import { VEHICLE_LIMITS, VL_LABELS, VL_MESSAGES } from '../constants';
import { useCompleteTrip } from '../hooks/useVehicleLog';
import type { VehicleTrip } from '../types';

const required = [{ required: true, message: MESSAGES.common.required }];

/** Có xăng thì phải có tiền xăng và ngược lại (khớp CompleteVehicleTripDtoValidator ở BE). */
const bothOrNeither =
  (other: keyof CompleteForm): Rule =>
  ({ getFieldValue }) => ({
    validator: async (_rule, value: number | null | undefined) => {
      const hasThis = value !== null && value !== undefined;
      const otherValue = getFieldValue(other) as number | null | undefined;
      if (hasThis !== (otherValue !== null && otherValue !== undefined))
        throw new Error(VL_MESSAGES.fuelBothRequired);
    },
  });

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
            <InputNumber
              min={trip.odometerStart + 1}
              max={VEHICLE_LIMITS.maxOdometer}
              precision={0}
              style={{ width: 200 }}
            />
          </Form.Item>
          <Form.Item
            name="fuelLiters"
            label={VL_LABELS.fuelLiters}
            dependencies={['fuelCost']}
            rules={[bothOrNeither('fuelCost')]}
          >
            <InputNumber
              min={0.01}
              max={VEHICLE_LIMITS.maxFuelLiters}
              precision={2}
              step={0.5}
              style={{ width: 200 }}
            />
          </Form.Item>
          <Form.Item
            name="fuelCost"
            label={VL_LABELS.fuelCost}
            dependencies={['fuelLiters']}
            rules={[bothOrNeither('fuelLiters')]}
          >
            <InputNumber
              min={1}
              max={VEHICLE_LIMITS.maxFuelCost}
              precision={0}
              step={1000}
              style={{ width: 200 }}
            />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
