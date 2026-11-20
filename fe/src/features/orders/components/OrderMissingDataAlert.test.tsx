import { render, screen } from '@testing-library/react';
import { MESSAGES } from '@/constants/messages';
import { OrderMissingDataAlert } from './OrderMissingDataAlert';

const text = MESSAGES.orders.orderExcel;

describe('OrderMissingDataAlert', () => {
  it('đơn thiếu bìa và NVL thì cảnh báo cả hai kèm gợi ý', () => {
    render(<OrderMissingDataAlert missingMarkers missingMaterials hint="Gợi ý" />);

    expect(screen.getByText(text.missingTitle)).toBeInTheDocument();
    expect(
      screen.getByText(`${text.missingMarkers} ${text.missingMaterials} Gợi ý`),
    ).toBeInTheDocument();
  });

  it('đơn đủ bìa và NVL thì không hiện gì', () => {
    const { container } = render(
      <OrderMissingDataAlert missingMarkers={false} missingMaterials={false} hint="Gợi ý" />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
