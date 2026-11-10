import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Empty, Flex, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import {
  distinctColors,
  emptyMarker,
  fabricOptionsFor,
  MAX_MARKERS,
  type MarkerRow,
} from '../utils/markerSpec';
import {
  dropRow,
  patchRow,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from '../utils/productSpec';
import { SpecMarkerCard } from './SpecMarkerCard';

const text = MESSAGES.orders.spec;

type SpecMarkersTabProps = {
  markers: MarkerRow[];
  setMarkers: Dispatch<SetStateAction<MarkerRow[]>>;
  variants: VariantRow[];
  norms: NormRow[];
  components: ComponentRow[];
  materialOptions: MaterialOption[];
  materialsLoading: boolean;
  /** Chế độ chỉ xem. Kế toán và Admin đều sửa được nên màn mã hàng không bật. */
  readOnly?: boolean;
};

/** Tab "Nhóm bìa": chi tiết cắt chung bìa và vải theo màu áo; bìa cụ thể (dài, tỉ lệ size) khai ở đơn hàng (DEV-ALL-85). */
export function SpecMarkersTab({
  markers,
  setMarkers,
  variants,
  norms,
  components,
  materialOptions,
  materialsLoading,
  readOnly = false,
}: SpecMarkersTabProps) {
  const colors = distinctColors(variants);
  const panelOptions = components
    .filter((c) => c.componentType === 'Panel' && c.componentName.trim())
    .map((c) => ({ value: c.key, label: c.componentName.trim() }));

  return (
    <>
      <Typography.Paragraph type="secondary">{text.markerHint}</Typography.Paragraph>
      {markers.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={text.markerEmpty} />
      )}
      <Flex vertical gap={12}>
        {markers.map((marker) => (
          <SpecMarkerCard
            key={marker.key}
            marker={marker}
            colors={colors}
            panelOptions={panelOptions}
            fabricOptions={(color) => fabricOptionsFor(color, norms, components, materialOptions)}
            materialsLoading={materialsLoading}
            readOnly={readOnly}
            onChange={(change) => patchRow(setMarkers, marker.key, change)}
            onRemove={() => dropRow(setMarkers, marker.key)}
          />
        ))}
      </Flex>
      {!readOnly && markers.length < MAX_MARKERS && (
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          style={{ marginTop: 12 }}
          onClick={() => setMarkers((rows) => [...rows, emptyMarker(rows)])}
        >
          {text.addMarker}
        </Button>
      )}
    </>
  );
}
