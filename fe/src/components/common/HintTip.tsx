import type { ReactNode } from 'react';
import { InfoCircleOutlined } from '@ant-design/icons';
import { Tooltip } from 'antd';

const HINT_MAX_WIDTH = 360;

type HintTipProps = {
  /** Nội dung hướng dẫn, ngắn gọn: chỉ hiện khi di chuột vào biểu tượng. */
  text: ReactNode;
};

/** Hướng dẫn thu gọn: biểu tượng (i), di chuột vào mới hiện nội dung, di chuột ra là ẩn. */
export function HintTip({ text }: HintTipProps) {
  return (
    <div className="hint-tip">
      <Tooltip title={text} styles={{ root: { maxWidth: HINT_MAX_WIDTH } }}>
        <InfoCircleOutlined aria-label="Hướng dẫn" />
      </Tooltip>
    </div>
  );
}
