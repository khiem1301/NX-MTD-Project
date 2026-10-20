import { useEffect } from 'react';
import { Form, Input, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { LABELS, MATERIAL_CATEGORIES, MATERIAL_TYPE, MATERIAL_TYPE_LABEL } from '../constants';
import { useSaveMaterial } from '../hooks/useMaterials';
import type { Material, SaveMaterialPayload } from '../types';

type MaterialFormModalProps = {
  open: boolean;
  material: Material | null;
  onClose: () => void;
};

const TYPE_OPTIONS = Object.values(MATERIAL_TYPE).map((t) => ({
  value: t,
  label: MATERIAL_TYPE_LABEL[t],
}));

export function MaterialFormModal({ open, material, onClose }: MaterialFormModalProps) {
  const [form] = Form.useForm<SaveMaterialPayload>();
  const save = useSaveMaterial();
  const materialType = Form.useWatch('materialType', form);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (material) form.setFieldsValue(material);
  }, [open, material, form]);

  const handleFinish = (values: SaveMaterialPayload) => {
    save.mutate(
      { id: material?.materialId, body: values },
      {
        onSuccess: onClose,
        onError: (error) =>
          form.setFields(
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({
              name: name as keyof SaveMaterialPayload,
              errors,
            })),
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      title={material ? `${MESSAGES.common.edit} ${material.materialCode}` : MESSAGES.common.create}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        {!material && (
          <Form.Item
            name="materialCode"
            label={LABELS.materialCode}
            extra={LABELS.materialCodeHint}
          >
            <Input maxLength={30} />
          </Form.Item>
        )}
        <Form.Item
          name="materialName"
          label={LABELS.materialName}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input maxLength={200} />
        </Form.Item>
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
        <Form.Item
          name="category"
          label={LABELS.category}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select options={materialType ? MATERIAL_CATEGORIES[materialType] : []} />
        </Form.Item>
        <Form.Item
          name="unit"
          label={LABELS.unit}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }]}
        >
          <Input maxLength={20} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
