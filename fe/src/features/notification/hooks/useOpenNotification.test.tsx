import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { NOTIFICATION_REF } from '../constants';
import { useNotificationModal } from './useNotificationModal';
import { hasNotificationAction, useOpenNotification } from './useOpenNotification';

const wrapper = ({ children }: { children: ReactNode }) => (
  <MemoryRouter initialEntries={['/orders']}>{children}</MemoryRouter>
);

const useOpenAndLocation = () => ({ open: useOpenNotification(), location: useLocation() });

const mismatch = { refTable: NOTIFICATION_REF.KPI_MISMATCH, refId: 7, linkUrl: null };

describe('useOpenNotification', () => {
  beforeEach(() => useNotificationModal.setState({ mismatchTargetId: null }));

  it('thông báo lệch kiểm cuối đơn mở cửa sổ chi tiết tại chỗ, không chuyển trang', () => {
    const { result } = renderHook(useOpenAndLocation, { wrapper });

    let handled = false;
    act(() => {
      handled = result.current.open(mismatch);
    });

    expect(handled).toBe(true);
    expect(useNotificationModal.getState().mismatchTargetId).toBe(7);
    expect(result.current.location.pathname).toBe('/orders');
  });

  it('thông báo có link thì chuyển trang, không mở cửa sổ', () => {
    const { result } = renderHook(useOpenAndLocation, { wrapper });

    act(() => {
      result.current.open({ refTable: null, refId: null, linkUrl: '/kpi/targets' });
    });

    expect(result.current.location.pathname).toBe('/kpi/targets');
    expect(useNotificationModal.getState().mismatchTargetId).toBeNull();
  });

  it('thông báo không có hành động thì không làm gì', () => {
    const { result } = renderHook(useOpenAndLocation, { wrapper });

    let handled = true;
    act(() => {
      handled = result.current.open({ refTable: null, refId: null, linkUrl: null });
    });

    expect(handled).toBe(false);
    expect(result.current.location.pathname).toBe('/orders');
  });
});

describe('hasNotificationAction', () => {
  it('có hành động khi là thông báo lệch có id hoặc có link', () => {
    expect(hasNotificationAction(mismatch)).toBe(true);
    expect(hasNotificationAction({ refTable: null, refId: null, linkUrl: '/x' })).toBe(true);
    expect(
      hasNotificationAction({
        refTable: NOTIFICATION_REF.KPI_MISMATCH,
        refId: null,
        linkUrl: null,
      }),
    ).toBe(false);
    expect(hasNotificationAction({ refTable: 'Announcement', refId: 3, linkUrl: null })).toBe(
      false,
    );
  });
});
