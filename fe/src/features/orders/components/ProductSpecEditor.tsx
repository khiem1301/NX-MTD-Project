import { useEffect, useState } from 'react';
import { Alert, Button, Tabs, Typography, message } from 'antd';
import { AuditHistoryTable } from '@/components/common/AuditHistoryTable';
import { ModalActions } from '@/components/common/DetailModal';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { fill } from '@/utils/format';
import { useSaveProductSpec } from '../hooks/useOrders';
import type { ProductDetail, ProductListItem, ProductSpec } from '../types';
import {
  toMarkerPayload,
  toMarkerRows,
  validateMarkers,
  type MarkerRow,
} from '../utils/markerSpec';
import {
  removeComponent,
  toMaterialOption,
  toPayload,
  toRows,
  validate,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { SpecComponentsTab } from './SpecComponentsTab';
import { SpecMarkersTab } from './SpecMarkersTab';
import { SpecNormsTab } from './SpecNormsTab';
import { SpecVariantsTab } from './SpecVariantsTab';

const text = MESSAGES.orders.spec;

type ProductSpecEditorProps = {
  product: ProductListItem;
  spec: ProductSpec;
  detail: ProductDetail | undefined;
  showHistory: boolean;
  /** Chế độ chỉ xem. Kế toán và Admin đều sửa được định mức nên màn mã hàng không bật. */
  readOnly?: boolean;
  onDirtyChange: (dirty: boolean) => void;
  onClose: () => void;
};

/** Phần soạn: khởi tạo từ dữ liệu máy chủ; lưu xong dữ liệu tải lại (đổi key) nên dựng lại từ bản đã lưu. */
export function ProductSpecEditor({
  product,
  spec,
  detail,
  showHistory,
  readOnly = false,
  onDirtyChange,
  onClose,
}: ProductSpecEditorProps) {
  const save = useSaveProductSpec();
  const materials = useMaterialOptions();
  const [initial] = useState(() => toRows(spec));
  const [components, setComponents] = useState<ComponentRow[]>(initial.components);
  const [variants] = useState<VariantRow[]>(initial.variants);
  const [norms, setNorms] = useState<NormRow[]>(initial.norms);
  const [markers, setMarkers] = useState<MarkerRow[]>(() => toMarkerRows(spec, initial.variants));
  const [error, setError] = useState('');

  const snapshot = JSON.stringify({ components, variants, norms, markers });
  const [baseline] = useState(snapshot);
  useEffect(() => {
    onDirtyChange(snapshot !== baseline);
    return () => onDirtyChange(false);
  }, [snapshot, baseline, onDirtyChange]);

  const materialOptions = (materials.data ?? []).map(toMaterialOption);

  const handleRemoveComponent = (key: string) => {
    const next = removeComponent(components, norms, key);
    setComponents(next.components);
    setNorms(next.norms);
    if (next.removedNorms > 0)
      void message.info(fill(MESSAGES.orders.specValidation.removedNorms, next.removedNorms));
  };

  const handleSave = () => {
    const problem =
      validate(components, variants, norms) ||
      validateMarkers(markers, variants, norms, components, materialOptions);
    setError(problem);
    if (problem) return;
    save.mutate({
      id: product.productId,
      body: { ...toPayload(components, variants, norms), ...toMarkerPayload(markers, variants) },
    });
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
        {!readOnly && (
          <Button type="primary" loading={save.isPending} onClick={handleSave}>
            {MESSAGES.common.save}
          </Button>
        )}
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
                onRemove={handleRemoveComponent}
                discontinued={discontinued}
                readOnly={readOnly}
              />
            ),
          },
          {
            key: 'variants',
            label: `${text.tabVariants} (${variants.length})`,
            children: (
              <SpecVariantsTab variants={variants} discontinuedVariants={discontinuedVariants} />
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
          {
            key: 'markers',
            label: `${text.tabMarkers} (${markers.length})`,
            children: (
              <SpecMarkersTab
                markers={markers}
                setMarkers={setMarkers}
                variants={variants}
                norms={norms}
                components={components}
                materialOptions={materialOptions}
                materialsLoading={materials.isLoading}
                readOnly={readOnly}
              />
            ),
          },
          ...(showHistory && detail
            ? [
                {
                  key: 'history',
                  label: MESSAGES.common.history,
                  children: <AuditHistoryTable history={detail.history} />,
                },
              ]
            : []),
        ]}
      />
    </>
  );
}
