import { fireEvent, render, screen } from '@testing-library/react';
import { HintTip } from './HintTip';

describe('HintTip', () => {
  it('ẩn nội dung; di chuột vào biểu tượng thì mới hiện', async () => {
    render(<HintTip text="Nội dung hướng dẫn" />);

    expect(screen.queryByText('Nội dung hướng dẫn')).not.toBeInTheDocument();

    fireEvent.mouseEnter(screen.getByLabelText('Hướng dẫn'));
    expect(await screen.findByText('Nội dung hướng dẫn')).toBeVisible();
  });
});
