import { render, screen } from '@testing-library/react';
import { ASSIGNMENT_MAX_OPERATIONS } from '../constants';
import { AssignmentBulkBar } from './AssignmentBulkBar';

jest.mock('../hooks/useProduction', () => ({
  useAssignOperations: () => ({ mutate: jest.fn(), isPending: false }),
}));

const ids = (count: number) => Array.from({ length: count }, (_, i) => i + 1);
const renderBar = (count: number) =>
  render(
    <AssignmentBulkBar
      orderId={1}
      operationIds={ids(count)}
      workerOptions={[]}
      onDone={jest.fn()}
    />,
  );

describe('AssignmentBulkBar: giới hạn số công đoạn', () => {
  it('đúng giới hạn thì không báo vượt', () => {
    renderBar(ASSIGNMENT_MAX_OPERATIONS);

    expect(screen.queryByText(/hãy bỏ bớt/)).not.toBeInTheDocument();
  });

  it('vượt giới hạn thì báo và khóa nút giao', () => {
    renderBar(ASSIGNMENT_MAX_OPERATIONS + 1);

    expect(screen.getByText(/tối đa 500 công đoạn/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Giao việc' })).toBeDisabled();
  });
});
