import type { ReactNode } from 'react';
import { Card, Col, Row, Skeleton, Typography } from 'antd';

export type StatTile = {
  key: string;
  label: string;
  value: ReactNode;
  suffix?: string;
  icon: ReactNode;
  /** Màu nhấn của ô; dùng token antd để hợp cả nền sáng và tối. */
  tone: 'primary' | 'success' | 'warning' | 'neutral';
};

const TONE_COLOR: Record<StatTile['tone'], string> = {
  primary: '#1677ff',
  success: '#52c41a',
  warning: '#fa8c16',
  neutral: '#8c8c8c',
};

/** Dải số liệu đầu màn hình: xem một cái là nắm được tình hình trong ngày. */
export function StatTiles({ tiles, loading }: { tiles: StatTile[]; loading?: boolean }) {
  return (
    <Row gutter={[16, 16]}>
      {tiles.map((tile) => (
        <Col key={tile.key} xs={12} md={12} lg={6}>
          <Card size="small" styles={{ body: { padding: 16 } }}>
            <Skeleton loading={loading} active paragraph={false} title={{ width: '70%' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span
                  aria-hidden
                  style={{
                    display: 'grid',
                    placeItems: 'center',
                    width: 40,
                    height: 40,
                    borderRadius: 10,
                    flex: '0 0 auto',
                    color: TONE_COLOR[tile.tone],
                    background: `${TONE_COLOR[tile.tone]}1a`,
                    fontSize: 18,
                  }}
                >
                  {tile.icon}
                </span>
                <div style={{ minWidth: 0 }}>
                  <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                    {tile.label}
                  </Typography.Text>
                  <div
                    style={{
                      fontSize: 22,
                      fontWeight: 600,
                      lineHeight: 1.2,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {tile.value}
                    {tile.suffix ? (
                      <Typography.Text type="secondary" style={{ fontSize: 14, marginLeft: 4 }}>
                        {tile.suffix}
                      </Typography.Text>
                    ) : null}
                  </div>
                </div>
              </div>
            </Skeleton>
          </Card>
        </Col>
      ))}
    </Row>
  );
}
