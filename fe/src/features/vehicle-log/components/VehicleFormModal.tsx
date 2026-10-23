import { useEffect } from 'react';
import { Form, Input, InputNumber, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { VL_LABELS } from '../constants';
import { useSaveVehicle } from '../hooks/useVehicleLog';
import type { SaveVehiclePayload, Vehicle } from '../types';

type VehicleFormModalProps = { open: boolean; vehicle: Vehicle | null; onClose: () => void };

/** UC35: Kế toán thêm / sửa xe (biển số duy nhất, định mức xăng lít / 100 km). */
export function VehicleFormModal({ open, vehicle, onClose }: VehicleFormModalProps) {
  const [form] = Form.useForm<SaveVehiclePayload>();
  const save = useSaveVehicle();
  const required = [{ required: true, whitespace: true, message: MESSAGES.common.required }];

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (vehicle)
      form.setFieldsValue({
        plateNumber: vehicle.plateNumber,
        vehicleType: vehicle.vehicleType,
        fuelNormPer100Km: vehicle.fuelNormPer100Km,
      });
  }, [open, vehicle, form]);

  return (
    <Modal
      open={open}
      title={vehicle ? VL_LABELS.editVehicle : VL_LABELS.addVehicle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={save.isPending}
        onFinish={(body) => save.mutate({ id: vehicle?.vehicleId, body }, { onSuccess: onClose })}
      >
        <Form.Item name="plateNumber" label={VL_LABELS.plate} rules={required}>
          <Input maxLength={20} />
        </Form.Item>
        <Form.Item name="vehicleType" label={VL_LABELS.vehicleType} rules={required}>
          <Input maxLength={50} />
        </Form.Item>
        <Form.Item
          name="fuelNormPer100Km"
          label={VL_LABELS.fuelNorm}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <InputNumber min={0.1} max={9999} step={0.5} style={{ width: 200 }} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
