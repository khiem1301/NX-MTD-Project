import { DeleteOutlined } from '@ant-design/icons';
import { Button, Card, Col, Row, Select, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { codeKey, type MarkerRow } from '../utils/markerSpec';
import type { MaterialOption } from '../utils/productSpec';

const text = MESSAGES.orders.spec;

/** Hàng của bảng vải: một màu áo. */
type ColorRow = { code: string };

type SpecMarkerCardProps = {
  marker: MarkerRow;
  colors: string[];
  /** Chi tiết cắt (Panel) chọn được cho nhóm. */
  panelOptions: { value: string; label: string }[];
  /** Vải chọn được cho từng màu áo: lấy từ định mức chi tiết cắt của màu đó. */
  fabricOptions: (color: string) => MaterialOption[];
  materialsLoading: boolean;
  readOnly: boolean;
  onChange: (change: Partial<MarkerRow>) => void;
  onRemove: () => void;
};

/** Một nhóm bìa: các chi tiết cắt chung bìa và vải theo từng màu áo. */
export function SpecMarkerCard({
  marker,
  colors,
  panelOptions,
  fabricOptions,
  materialsLoading,
  readOnly,
  onChange,
  onRemove,
}: SpecMarkerCardProps) {
  const no = marker.markerNo;
  return (
    <Card
      size="small"
      title={fill(text.markerTitle, no)}
      extra={
        !readOnly && (
          <Button
            size="small"
            danger
            icon={<DeleteOutlined />}
            aria-label={fill(text.removeMarker, no)}
            onClick={onRemove}
          />
        )
      }
    >
      <Row gutter={16}>
        <Col xs={24} md={12}>
          <Typography.Text strong>{text.markerComponents}</Typography.Text>
          <Select
            mode="multiple"
            disabled={readOnly}
            aria-label={fill(text.markerComponentsOf, no)}
            placeholder={text.markerComponentsPlaceholder}
            value={marker.componentKeys}
            options={panelOptions}
            optionFilterProp="label"
            onChange={(componentKeys: string[]) => onChange({ componentKeys })}
            style={{ width: '100%', marginTop: 8 }}
          />
        </Col>
        <Col xs={24} md={12}>
          <DataTable<ColorRow>
            rowKey="code"
            size="small"
            pagination={false}
            dataSource={colors.map((code) => ({ code }))}
            columns={[
              { title: text.color, width: 110, dataIndex: 'code' },
              {
                title: text.markerFabric,
                key: 'fabric',
                render: (_, { code: color }) => (
                  <Select
                    size="small"
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    optionLabelProp="short"
                    disabled={readOnly}
                    aria-label={`${text.markerFabric} ${color}`}
                    placeholder={text.markerFabricPlaceholder}
                    value={marker.fabrics[codeKey(color)]}
                    options={fabricOptions(color)}
                    loading={materialsLoading}
                    popupMatchSelectWidth={false}
                    onChange={(value?: number) =>
                      onChange({ fabrics: { ...marker.fabrics, [codeKey(color)]: value } })
                    }
                    style={{ width: '100%' }}
                  />
                ),
              },
            ]}
          />
        </Col>
      </Row>
    </Card>
  );
}
