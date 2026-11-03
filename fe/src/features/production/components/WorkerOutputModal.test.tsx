import { fireEvent, render, screen } from '@testing-library/react';
import type { OutputGridRow } from '../types';
import { WorkerOutputModal } from './WorkerOutputModal';

const row = (over: Partial<OutputGridRow>): OutputGridRow => ({
  operationId: 1,
  operationCode: 'OP1',
  operationName: 'May túi',
  groupName: 'Túi',
  workerId: 7,
  workerCode: 'CN1',
  workerName: 'An',
  outputRecordId: null,
  quantity: null,
  unitRate: 1000,
  rateApproved: true,
  isOutsideAssignment: false,
  noAttendanceWarning: false,
  canEdit: true,
  editDeadline: null,
  rowVer: null,
  capQty: 100,
  reportedQty: 0,
  ...over,
});

const OP2 = row({ operationId: 2, operationCode: 'OP2' });
const ROWS = [
  row({ operationId: 1, operationCode: 'OP1' }),
  OP2,
  row({ operationId: 3, operationCode: 'OP3', groupName: 'Tay' }),
];

const setup = (rows: OutputGridRow[] = ROWS) => {
  const onSave = jest.fn();
  render(
    <WorkerOutputModal
      worker={{ workerId: 7, label: 'CN1 - An' }}
      rows={rows}
      saving={false}
      onSave={onSave}
      onClose={jest.fn()}
    />,
  );
  return onSave;
};

const input = (name: string) => screen.getByRole('spinbutton', { name }) as HTMLInputElement;
const type = (name: string, value: string) => fireEvent.change(input(name), { target: { value } });
const saveButton = () => screen.getByRole('button', { name: /Lưu sản lượng/ });

describe('WorkerOutputModal', () => {
  it('nhập sản lượng nhóm thì điền cho mọi công đoạn trong nhóm, không đụng nhóm khác', () => {
    setup();
    type('Sản lượng nhóm Túi', '20');

    expect(input('Sản lượng OP1').value).toBe('20');
    expect(input('Sản lượng OP2').value).toBe('20');
    expect(input('Sản lượng OP3').value).toBe('');
  });

  it('sửa lại từng công đoạn sau khi điền nhóm và lưu đúng số đã sửa', () => {
    const onSave = setup();
    type('Sản lượng nhóm Túi', '20');
    type('Sản lượng OP2', '15');
    fireEvent.click(saveButton());

    expect(onSave).toHaveBeenCalledWith([
      { operationId: 1, workerId: 7, quantity: 20, rowVer: null },
      { operationId: 2, workerId: 7, quantity: 15, rowVer: null },
    ]);
  });

  it('không điền vào công đoạn đã hết hạn sửa', () => {
    setup([row({ operationId: 1, operationCode: 'OP1', quantity: 5, canEdit: false }), OP2]);
    type('Sản lượng nhóm Túi', '20');

    expect(input('Sản lượng OP1').value).toBe('5');
    expect(input('Sản lượng OP2').value).toBe('20');
  });

  it('chỉ điền vào công đoạn lưu được: bỏ qua chưa có đơn giá, đã báo hết', () => {
    setup([
      row({ operationId: 1, operationCode: 'OP1' }),
      row({ operationId: 3, operationCode: 'OP3', rateApproved: false }),
      row({ operationId: 4, operationCode: 'OP4', capQty: 5, reportedQty: 5 }),
    ]);
    expect(screen.getByText('Nhóm Túi (nhập được 1/3 công đoạn)')).toBeInTheDocument();

    type('Sản lượng nhóm Túi', '20');

    expect(input('Sản lượng OP1').value).toBe('20');
    expect(input('Sản lượng OP3').value).toBe('');
    expect(input('Sản lượng OP3')).toBeDisabled();
    expect(input('Sản lượng OP4').value).toBe('');
    expect(saveButton()).toBeEnabled();
  });

  it('vượt số còn được báo thì báo rõ và khóa nút lưu; không tính lại số đã nhập của chính dòng', () => {
    setup([
      row({ operationId: 1, operationCode: 'OP1', quantity: 10, reportedQty: 90, capQty: 100 }),
    ]);

    type('Sản lượng OP1', '20');
    expect(saveButton()).toBeEnabled();

    type('Sản lượng OP1', '21');
    expect(screen.getByText('OP1: chỉ còn được báo 20.')).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('không có ô chọn thêm công đoạn; chưa được giao việc thì báo cần giao ở màn Giao việc', () => {
    setup([]);

    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.getByText(/chưa được giao việc nào ở dòng đơn này/)).toBeInTheDocument();
  });

  it('số 0 ở dòng đã nhập được gửi để xóa; ô để trống không gửi', () => {
    const onSave = setup([row({ operationId: 1, operationCode: 'OP1', quantity: 10 }), OP2]);
    type('Sản lượng OP1', '0');
    fireEvent.click(saveButton());

    expect(onSave).toHaveBeenCalledWith([
      { operationId: 1, workerId: 7, quantity: 0, rowVer: null },
    ]);
  });
});
