import { initials } from './menuModel';

export function UserAvatar({ name, size = 36 }: { name: string; size?: number }) {
  return (
    <span
      className="app-avatar"
      style={{ width: size, height: size, fontSize: Math.round(size * 0.4) }}
      aria-hidden
    >
      {initials(name)}
    </span>
  );
}
