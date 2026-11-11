import { useState } from 'react';
import { INCOMING_TAB } from '../constants';
import { EMPTY_INCOMING_FILTER, type IncomingFilter } from '../utils/filterIncoming';

type IncomingTab = (typeof INCOMING_TAB)[keyof typeof INCOMING_TAB];

const INITIAL: Record<IncomingTab, IncomingFilter> = {
  [INCOMING_TAB.CUSTOMER]: EMPTY_INCOMING_FILTER,
  [INCOMING_TAB.SUPPLIER]: EMPTY_INCOMING_FILTER,
  [INCOMING_TAB.REPLENISH]: EMPTY_INCOMING_FILTER,
  [INCOMING_TAB.CLOSED_SHORT]: EMPTY_INCOMING_FILTER,
};

/** Bộ lọc riêng của từng tab màn NVL chờ nhận: đổi tab không mất bộ lọc của tab kia. */
export const useIncomingFilters = () => {
  const [filters, setFilters] = useState(INITIAL);
  const patch = (tab: IncomingTab, change: Partial<IncomingFilter>) =>
    setFilters((current) => ({ ...current, [tab]: { ...current[tab], ...change } }));
  return { filters, patch };
};
