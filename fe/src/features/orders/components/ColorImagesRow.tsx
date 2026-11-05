import { DeleteOutlined } from '@ant-design/icons';
import { Button, Flex, Form, theme } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { MAX_COLOR_IMAGES } from '../constants';
import { colorImageCountError, colorNameError } from '../utils/productValidation';
import { ColorAutoComplete } from './ColorAutoComplete';
import { ExtraImagesInput } from './ExtraImagesInput';

const text = MESSAGES.orders;
const LIST = 'colorImages';
const COLOR_THUMB = 72;
const COLOR_INPUT_WIDTH = 240;

type ColorImagesRowProps = {
  /** Vị trí dòng trong Form.List (field.name). */
  name: number;
  suggestions: readonly string[];
  onRemove: () => void;
};

/** Một màu trong "Ảnh theo màu": ô màu + nút bỏ màu ở hàng trên, ảnh của màu ở hàng dưới; lỗi hiện ngay dưới từng ô. */
export function ColorImagesRow({ name, suggestions, onRemove }: ColorImagesRowProps) {
  const form = Form.useFormInstance();
  const { token } = theme.useToken();

  return (
    <div
      style={{
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        padding: token.paddingSM,
      }}
    >
      <Flex gap={8} align="start" justify="space-between" wrap>
        <Form.Item
          name={[name, 'color']}
          style={{ marginBottom: 0, width: COLOR_INPUT_WIDTH, maxWidth: '100%' }}
          rules={[
            {
              validator: async () => {
                const error = colorNameError(form.getFieldValue(LIST), name);
                if (error) throw new Error(error);
              },
            },
          ]}
        >
          <ColorAutoComplete placeholder={text.colorPlaceholder} extraColors={suggestions} />
        </Form.Item>
        <Button type="text" danger icon={<DeleteOutlined />} onClick={onRemove}>
          {text.removeColor}
        </Button>
      </Flex>
      <Form.Item
        name={[name, 'images']}
        style={{ marginTop: token.marginSM, marginBottom: 0 }}
        rules={[
          {
            validator: async (_, images: string[] | undefined) => {
              const error = colorImageCountError(images);
              if (error) throw new Error(error);
            },
          },
        ]}
      >
        <ExtraImagesInput
          max={MAX_COLOR_IMAGES}
          countText={text.colorImagesCount}
          fullText={text.colorImagesFull}
          thumb={COLOR_THUMB}
        />
      </Form.Item>
    </div>
  );
}
