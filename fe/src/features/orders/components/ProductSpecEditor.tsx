import { useEffect, useState } from 'react';
import { Alert, Button, Tabs, Typography } from 'antd';
import { AuditHistory } from '@/components/common/AuditHistory';
import { ModalActions } from '@/components/common/DetailModal';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { useSaveProductSpec } from '../hooks/useOrders';
import type { ProductDetail, ProductListItem, ProductSpec } from '../types';
import {
  toPayload,
  toRows,
  validate,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { SpecComponentsTab } from './SpecComponentsTab';
import { SpecNormsTab } from './SpecNormsTab';
import { SpecVariantsTab } from './SpecVariantsTab';

const text = MESSAGES.orders.spec;

type ProductSpecEditorProps = {
  product: ProductListItem;
  spec: ProductSpec;
  detail: ProductDetail | undefined;
  showHistory: boolean;
  onDirtyChange: (dirty: boolean) => void;
  onClose: () => void;
};

/** Phần soạn: khởi tạo từ dữ liệu máy chủ; lưu xong dữ liệu tải lại (đổi key) nên dựng lại từ bản đã lưu. */
export function ProductSpecEditor({
  product,
  spec,
  detail,
  showHistory,
  onDirtyChange,
  onClose,
}: ProductSpecEditorProps) {
  const save = useSaveProductSpec();
  const materials = useMaterialOptions();
  const [initial] = useState(() => toRows(spec));
  const [components, setComponents] = useState<ComponentRow[]>(initial.components);
  const [variants, setVariants] = useState<VariantRow[]>(initial.variants);
  const [norms, setNorms] = useState<NormRow[]>(initial.norms);
  const [error, setError] = useState('');

  const snapshot = JSON.stringify({ components, variants, norms });
  const [baseline] = useState(snapshot);
  useEffect(() => {
    onDirtyChange(snapshot !== baseline);
    return () => onDirtyChange(false);
  }, [snapshot, baseline, onDirtyChange]);

  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
  }));

  const handleSave = () => {
    const problem = validate(components, variants, norms);
    setError(problem);
    if (problem) return;
    save.mutate({ id: product.productId, body: toPayload(components, variants, norms) });
  };

  const discontinued = spec.components
    .filter((c) => c.status !== 'Active')
    .map((c) => c.componentName);
  const discontinuedVariants = spec.variants
    .filter((v) => v.status !== 'Active')
    .map((v) => `${v.color}-${v.sizeCode}`);

  return (
    <>
      <ModalActions>
        <Button onClick={onClose}>{MESSAGES.common.close}</Button>
        <Button type="primary" loading={save.isPending} onClick={handleSave}>
          {MESSAGES.common.save}
        </Button>
      </ModalActions>
      {detail?.description && (
        <Typography.Paragraph type="secondary">{`${MESSAGES.orders.description}: ${detail.description}`}</Typography.Paragraph>
      )}
      {error && <Alert type="warning" showIcon message={error} style={{ marginBottom: 12 }} />}
      <Tabs
        items={[
          {
            key: 'components',
            label: `${text.tabComponents} (${components.length})`,
            children: (
              <SpecComponentsTab
                components={components}
                setComponents={setComponents}
                discontinued={discontinued}
              />
            ),
          },
          {
            key: 'variants',
            label: `${text.tabVariants} (${variants.length})`,
            children: (
              <SpecVariantsTab
                variants={variants}
                setVariants={setVariants}
                discontinuedVariants={discontinuedVariants}
              />
            ),
          },
          {
            key: 'norms',
            label: `${text.tabNorms} (${norms.length})`,
            children: (
              <SpecNormsTab
                norms={norms}
                setNorms={setNorms}
                variants={variants}
                components={components}
                materialOptions={materialOptions}
                materialsLoading={materials.isLoading}
              />
            ),
          },
          ...(showHistory && detail
            ? [
                {
                  key: 'history',
                  label: MESSAGES.common.history,
                  children: <AuditHistory history={detail.history} />,
                },
              ]
            : []),
        ]}
      />
    </>
  );
}
