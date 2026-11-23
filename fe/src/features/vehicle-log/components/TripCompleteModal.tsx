import { useEffect } from 'react';
import { Form, InputNumber, Modal } from 'antd';
import { AttachmentGallery } from '@/components/common/attachments';
import { QtyInput } from '@/components/common/QtyInput';
import type { Rule } from 'antd/es/form';
import { MESSAGES } from '@/constants/messages';
import { useAttachments } from '@/hooks/useAttachments';
import { ATTACHMENT_OWNER, ATTACHMENT_PURPOSE } from '@/types/attachment';
import { fill } from '@/utils/format';
import { FUEL_PHOTO_MAX, VEHICLE_LIMITS, VL_LABELS, VL_MESSAGES } from '../constants';
import { useCompleteTrip } from '../hooks/useVehicleLog';
import type { VehicleTrip } from '../types';

const required = [{ required: true, message: MESSAGES.common.required }];

/** DEV-ALL-130: km cuối nhỏ hơn hoặc bằng km đầu thì báo lỗi dưới ô, không lưu. */
const endAfterStart = (start: number): Rule => ({
  validator: async (_rule, value: number | null | undefined) => {
    if (value !== null && value !== undefined && value <= start)
      throw new Error(fill(VL_MESSAGES.odometerEndTooSmall, start));
  },
});

type CompleteForm = { odometerEnd: number; fuelCost?: number | null };

/** UC35: kết thúc chuyến (km cuối, tiền xăng). DEV-ALL-171: có tiền xăng thì phải có ảnh hóa đơn xăng (tải ngay trong popup). */
export function TripCompleteModal({
  trip,
  onClose,
}: {
  trip: VehicleTrip | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<CompleteForm>();
  const complete = useCompleteTrip();
  const fuelCost = Form.useWatch('fuelCost', form);
  const photos = useAttachments(ATTACHMENT_OWNER.VehicleTrip, trip?.vehicleTripId ?? null);
  const hasReceipt = (photos.data ?? []).some((a) => a.purpose === ATTACHMENT_PURPOSE.FuelReceipt);
  const needsReceipt = fuelCost !== null && fuelCost !== undefined;

  // Tải xong ảnh (hoặc gỡ ảnh) thì kiểm lại ô tiền xăng để lỗi tự mất / hiện lại.
  useEffect(() => {
    if (form.isFieldTouched('fuelCost'))
      void form.validateFields(['fuelCost']).catch(() => undefined);
  }, [hasReceipt, form]);

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
          <Form.Item
            name="odometerEnd"
            label={VL_LABELS.odometerEnd}
            rules={[...required, endAfterStart(trip.odometerStart)]}
          >
            <QtyInput min={0} max={VEHICLE_LIMITS.maxOdometer} style={{ width: 200 }} />
          </Form.Item>
          <Form.Item
            name="fuelCost"
            label={VL_LABELS.fuelCost}
            rules={[
              {
                validator: async (_rule, value: number | null | undefined) => {
                  if (value !== null && value !== undefined && !hasReceipt)
                    throw new Error(VL_MESSAGES.fuelReceiptRequired);
                },
              },
            ]}
          >
            <QtyInput min={1} max={VEHICLE_LIMITS.maxFuelCost} step={1000} style={{ width: 200 }} />
          </Form.Item>
          {needsReceipt && (
            <Form.Item label={VL_LABELS.receiptPhotos} required>
              <AttachmentGallery
                ownerType={ATTACHMENT_OWNER.VehicleTrip}
                ownerId={trip.vehicleTripId}
                canEdit
                maxFiles={FUEL_PHOTO_MAX}
                purpose={ATTACHMENT_PURPOSE.FuelReceipt}
                onlyPurpose
              />
            </Form.Item>
          )}
        </Form>
      )}
    </Modal>
  );
}
