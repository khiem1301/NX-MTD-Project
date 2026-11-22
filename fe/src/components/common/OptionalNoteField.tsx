import { useEffect, useState, type ReactNode } from 'react';
import { Button, Form, type FormItemProps } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';

type Props = {
  name?: string;
  label?: string;
  /** Hiện sẵn khi sửa bản ghi đã có ghi chú. */
  defaultOpen?: boolean;
  rules?: FormItemProps['rules'];
  children: ReactNode;
  style?: React.CSSProperties;
};

/** C8 — ô ghi chú thu gọn: bấm "+ Thêm ghi chú" mới hiện. */
export function OptionalNoteField({
  name = 'note',
  label = MESSAGES.common.note,
  defaultOpen = false,
  rules,
  children,
  style,
}: Props) {
  const form = Form.useFormInstance();
  const watched = Form.useWatch(name, form) as string | undefined;
  const [open, setOpen] = useState(defaultOpen);

  useEffect(() => {
    if (defaultOpen || (typeof watched === 'string' && watched.trim())) {
      setOpen(true);
    }
  }, [defaultOpen, watched]);

  if (!open) {
    return (
      <Button
        type="link"
        icon={<PlusOutlined />}
        onClick={() => setOpen(true)}
        style={{ paddingLeft: 0, marginBottom: 12, ...style }}
      >
        {MESSAGES.common.addNote}
      </Button>
    );
  }

  return (
    <Form.Item name={name} label={label} rules={rules} style={style}>
      {children}
    </Form.Item>
  );
}
