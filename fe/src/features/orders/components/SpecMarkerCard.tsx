import { DeleteOutlined } from '@ant-design/icons';
import { Button, Card, Col, InputNumber, Row, Select } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { codeKey, type MarkerRow, type MarkerSize } from '../utils/markerSpec';
import { MAX_SIZE_CM, type MaterialOption } from '../utils/productSpec';

const text = MESSAGES.orders.spec;

/** Hàng của bảng con: một size hoặc một màu. */
type CodeRow = { code: string };
const toRow = (code: string): CodeRow => ({ code });

type SpecMarkerCardProps = {
  marker: MarkerRow;
  no: number;
  sizes: string[];
  colors: string[];
  /** Vải chọn được cho từng màu áo: lấy từ định mức chi tiết cắt của màu đó. */
  fabricOptions: (color: string) => MaterialOption[];
  materialsLoading: boolean;
  readOnly: boolean;
  onChange: (change: Partial<MarkerRow>) => void;
  onRemove: () => void;
};

/** Một bìa: kích thước theo từng size và vải theo từng màu áo. */
export function SpecMarkerCard({
  marker,
  no,
  sizes,
  colors,
  fabricOptions,
  materialsLoading,
  readOnly,
  onChange,
  onRemove,
}: SpecMarkerCardProps) {
  const sizeOf = (size: string): MarkerSize =>
    marker.sizes[codeKey(size)] ?? { lengthCm: null, widthCm: null };
  const patchSize = (size: string, change: Partial<MarkerSize>) =>
    onChange({
      sizes: { ...marker.sizes, [codeKey(size)]: { ...sizeOf(size), ...change } },
    });
  const sizeInput = (size: string, field: keyof MarkerSize, label: string) => (
    <InputNumber
      size="small"
      min={0.01}
      max={MAX_SIZE_CM}
      precision={2}
      disabled={readOnly}
      aria-label={`${label} ${size}`}
      value={sizeOf(size)[field]}
      onChange={(value) => patchSize(size, { [field]: value })}
      style={{ width: '100%' }}
    />
  );

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
          <DataTable<CodeRow>
            rowKey="code"
            size="small"
            pagination={false}
            dataSource={sizes.map(toRow)}
            columns={[
              { title: text.size, width: 90, dataIndex: 'code' },
              {
                title: text.length,
                key: 'length',
                render: (_, { code }) => sizeInput(code, 'lengthCm', text.length),
              },
              {
                title: text.width,
                key: 'width',
                render: (_, { code }) => sizeInput(code, 'widthCm', text.width),
              },
            ]}
          />
        </Col>
        <Col xs={24} md={12}>
          <DataTable<CodeRow>
            rowKey="code"
            size="small"
            pagination={false}
            dataSource={colors.map(toRow)}
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
