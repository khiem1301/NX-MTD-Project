import { Typography } from 'antd';

type Props = {
  code: string;
  name: string;
  /** Màu NVL; trống thì hiện —. */
  color?: string | null;
  /** ĐVT; bỏ qua nếu đã gắn vào cột số lượng. */
  unit?: string | null;
};

/** C5 — một ô NVL: dòng 1 `Mã · Tên`, dòng phụ `Màu · ĐVT`. */
export function MaterialIdentityCell({ code, name, color, unit }: Props) {
  const secondary = [color || '—', unit || undefined].filter((v) => v !== undefined).join(' · ');
  return (
    <div>
      <div>{`${code} · ${name}`}</div>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {secondary}
      </Typography.Text>
    </div>
  );
}
