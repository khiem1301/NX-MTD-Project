import { Modal, type ModalFuncProps } from 'antd';
import { MESSAGES } from '@/constants/messages';

/** Modal.confirm gọi tĩnh không nhận ConfigProvider (luôn "OK / Cancel"), nên đặt sẵn nhãn tiếng Việt ở đây. */
export const confirmDialog = (props: ModalFuncProps) =>
  Modal.confirm({ okText: MESSAGES.common.confirm, cancelText: MESSAGES.common.cancel, ...props });
