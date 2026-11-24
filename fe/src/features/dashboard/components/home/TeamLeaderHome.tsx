import { useTeamLeaderHome } from '../../hooks/useHome';
import { CuttingHome } from './CuttingHome';
import { HomeState } from './HomeState';
import { SewingHome } from './SewingHome';

/** Trang chủ tổ trưởng / tổ phó: tổ cắt hoặc tổ may tùy loại tổ (BE trả đúng khối của tổ đó). */
export function TeamLeaderHome() {
  const query = useTeamLeaderHome();
  return (
    <HomeState query={query}>
      {(d) => (
        <>
          {d.cutting && <CuttingHome data={d.cutting} />}
          {d.sewing && <SewingHome data={d.sewing} />}
        </>
      )}
    </HomeState>
  );
}
