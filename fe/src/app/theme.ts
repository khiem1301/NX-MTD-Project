import type { ThemeConfig } from 'antd';

/**
 * Giao diện chung: màn xưởng dùng trên điện thoại nên chữ và vùng bấm phải đủ lớn
 * (Rules FE: nút to, tối đa 3 thao tác).
 */
export const antdTheme: ThemeConfig = {
  token: {
    colorPrimary: '#1677ff',
    colorSuccess: '#52c41a',
    colorWarning: '#fa8c16',
    colorError: '#ff4d4f',
    borderRadius: 8,
    fontSize: 15,
    controlHeight: 38,
  },
  components: {
    Table: {
      headerBg: '#fafafa',
      headerColor: '#434343',
      rowHoverBg: '#f0f7ff',
      cellPaddingBlock: 12,
    },
    Layout: {
      siderBg: '#001529',
      bodyBg: '#f5f7fa',
      headerBg: '#ffffff',
      headerPadding: '0 24px',
      headerHeight: 64,
    },
    Menu: {
      darkItemBg: '#001529',
      darkSubMenuItemBg: '#000c17',
      darkItemSelectedBg: '#1677ff',
      darkItemHoverBg: 'rgba(255, 255, 255, 0.08)',
      itemBorderRadius: 8,
      itemMarginInline: 8,
      itemHeight: 42,
      iconSize: 16,
      collapsedIconSize: 18,
    },
    Card: {
      boxShadowTertiary: '0 1px 2px rgba(0, 0, 0, 0.04)',
    },
    Tabs: {
      titleFontSize: 15,
    },
  },
};
