import type { Dispatch, SetStateAction } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Empty, Flex, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import {
  distinctColors,
  distinctSizes,
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

/** Tab "Bìa": kích thước bìa theo size và vải của bìa theo màu áo, dùng để tính số mét vải của lệnh cắt cả bộ. */
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
  const sizes = distinctSizes(variants);
  const colors = distinctColors(variants);

  return (
    <>
      <Typography.Paragraph type="secondary">{text.markerHint}</Typography.Paragraph>
      {markers.length === 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={text.markerEmpty} />
      )}
      <Flex vertical gap={12}>
        {markers.map((marker, index) => (
          <SpecMarkerCard
            key={marker.key}
            marker={marker}
            no={index + 1}
            sizes={sizes}
            colors={colors}
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
          onClick={() => setMarkers((rows) => [...rows, emptyMarker()])}
        >
          {text.addMarker}
        </Button>
      )}
    </>
  );
}
