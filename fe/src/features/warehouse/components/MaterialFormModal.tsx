import { useEffect } from 'react';
import { Col, Form, Input, InputNumber, Modal, Row, Select, Typography } from 'antd';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { FABRIC_UNIT } from '@/constants/materials';
import { MESSAGES } from '@/constants/messages';
import { LABELS, MATERIAL_CATEGORIES, MATERIAL_TYPE, MATERIAL_TYPE_LABEL } from '../constants';
import { useSaveMaterial } from '../hooks/useMaterials';
import type { Material, SaveMaterialPayload } from '../types';
import { cmToMeters, MAX_FABRIC_WIDTH_M, metersToCm } from '../utils/fabricWidth';

type MaterialFormModalProps = {
  open: boolean;
  material: Material | null;
  onClose: () => void;
  /** Gọi sau khi lưu xong, kèm NVL vừa lưu (để chọn luôn ở form gọi tới). */
  onSaved?: (material: Material) => void;
};

/** Khổ vải trên form nhập bằng mét; API nhận cm (widthCm). */
type FormValues = Omit<SaveMaterialPayload, 'widthCm'> & { widthM?: number | null };

const TYPE_OPTIONS = Object.values(MATERIAL_TYPE).map((t) => ({
  value: t,
  label: MATERIAL_TYPE_LABEL[t],
}));

export function MaterialFormModal({ open, material, onClose, onSaved }: MaterialFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveMaterial();
  const materialType = Form.useWatch('materialType', form);
  const isFabric = materialType === MATERIAL_TYPE.FABRIC;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (material) form.setFieldsValue({ ...material, widthM: cmToMeters(material.widthCm) });
  }, [open, material, form]);

  // Vải cố định đơn vị mét, không cho sửa.
  useEffect(() => {
    if (isFabric) form.setFieldValue('unit', FABRIC_UNIT);
  }, [isFabric, form]);

  const handleFinish = ({ widthM, ...values }: FormValues) => {
    const body: SaveMaterialPayload = {
      ...values,
      widthCm: values.materialType === MATERIAL_TYPE.FABRIC ? metersToCm(widthM) : null,
    };
    save.mutate(
      { id: material?.materialId, body },
      {
        onSuccess: (saved) => {
          onSaved?.(saved as Material);
          onClose();
        },
        onError: (error) =>
          form.setFields(
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({
              name: (name === 'widthCm' ? 'widthM' : name) as keyof FormValues,
              errors,
            })),
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={material ? `${MESSAGES.common.edit} ${material.materialCode}` : MESSAGES.common.create}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Row gutter={16}>
          {!material && (
            <Col xs={24} sm={8}>
              <Form.Item
                name="materialCode"
                label={LABELS.materialCode}
                extra={LABELS.materialCodeHint}
              >
                <Input maxLength={30} />
              </Form.Item>
            </Col>
          )}
          <Col xs={24} sm={material ? 24 : 16}>
            <Form.Item
              name="materialName"
              label={LABELS.materialName}
              rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
            >
              <Input maxLength={200} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="materialType"
              label={LABELS.materialType}
              rules={[{ required: true, message: MESSAGES.common.required }]}
            >
              <Select
                options={TYPE_OPTIONS}
                onChange={() => form.setFieldValue('category', undefined)}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="category"
              label={LABELS.category}
              rules={[{ required: true, message: MESSAGES.common.required }]}
            >
              <Select options={materialType ? MATERIAL_CATEGORIES[materialType] : []} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="unit"
              label={LABELS.unit}
              rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
            >
              <Input maxLength={20} disabled={isFabric} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="color" label={LABELS.color}>
              <Input maxLength={50} />
            </Form.Item>
          </Col>
          <Col xs={24}>
            <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
              {LABELS.materialColorHint}
            </Typography.Paragraph>
          </Col>
          {/* Phụ liệu (cúc, khóa, chỉ...) tính theo đơn vị đếm, không có khổ: chỉ vải mới nhập khổ vải. */}
          {isFabric && (
            <>
              <Col xs={24} sm={12}>
                <Form.Item
                  name="widthM"
                  label={LABELS.materialWidthM}
                  rules={[{ required: true, message: MESSAGES.common.required }]}
                >
                  <InputNumber
                    min={0.01}
                    max={MAX_FABRIC_WIDTH_M}
                    precision={2}
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              </Col>
              <Col xs={24}>
                <Typography.Paragraph type="secondary" style={{ marginTop: -8, marginBottom: 0 }}>
                  {LABELS.materialSizeHint}
                </Typography.Paragraph>
              </Col>
            </>
          )}
        </Row>
      </Form>
    </Modal>
  );
}
