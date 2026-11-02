import { createContext } from 'react';

/** true khi nằm trong PageCard: PageCard đã giãn cách các khối nên TableToolbar không thêm margin dưới. */
export const InPageCardContext = createContext(false);
