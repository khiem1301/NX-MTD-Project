import { Col, Form, Row } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ExtraImagesInput } from './ExtraImagesInput';
import { SampleImageInput } from './SampleImageInput';
import { HintTip } from '@/components/common/HintTip';

/** Cỡ ô ảnh trong form (nhỏ hơn mặc định để form gọn) và khoảng cách tới dòng gợi ý định dạng ảnh. */
const IMAGE_THUMB = 56;
const IMAGE_ROW_GAP = 4;
const SAMPLE_IMAGE_MAX_LENGTH = 500;

/**
 * Ảnh của form mã hàng, gọn một hàng: ảnh chính bên trái, ảnh phụ (ô nhỏ) bên phải, một dòng gợi ý định dạng chung. Đặt trong
 * Form của mã hàng (field sampleImageUrl, extraImages).
 */
export function ProductImagesFields() {
  return (
    <>
      <Row gutter={16}>
        <Col xs={24} md={9}>
          <Form.Item
            name="sampleImageUrl"
            label={MESSAGES.orders.sampleImage}
            rules={[{ max: SAMPLE_IMAGE_MAX_LENGTH }]}
            style={{ marginBottom: IMAGE_ROW_GAP }}
          >
            <SampleImageInput hideHint size={IMAGE_THUMB} />
          </Form.Item>
        </Col>
        <Col xs={24} md={15}>
          <Form.Item
            name="extraImages"
            label={MESSAGES.orders.extraImages}
            style={{ marginBottom: IMAGE_ROW_GAP }}
          >
            <ExtraImagesInput
              thumb={IMAGE_THUMB}
              countText={MESSAGES.orders.extraImagesCountShort}
            />
          </Form.Item>
        </Col>
      </Row>
      <HintTip text={MESSAGES.orders.imageHintAll} />
    </>
  );
}
