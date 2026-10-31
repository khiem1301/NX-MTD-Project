export function Brand({ collapsed, onClick }: { collapsed: boolean; onClick: () => void }) {
  return (
    <div
      className={collapsed ? 'app-brand app-brand--collapsed' : 'app-brand'}
      onClick={onClick}
      role="link"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onClick()}
    >
      <div className="app-brand__logo">NX</div>
      {!collapsed && (
        <div>
          <div className="app-brand__name">NX-MTD</div>
          <div className="app-brand__tag">Quản lý xưởng may</div>
        </div>
      )}
    </div>
  );
}
