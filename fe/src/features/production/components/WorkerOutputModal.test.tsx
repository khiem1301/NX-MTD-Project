import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { confirmDialog } from '@/utils/confirmDialog';
import type { OutputGridRow } from '../types';
import { WorkerOutputModal } from './WorkerOutputModal';

jest.mock('@/utils/confirmDialog', () => ({
  confirmDialog: jest.fn((props: { onOk?: () => void }) => props.onOk?.()),
}));

beforeAll(() => {
  Element.prototype.scrollIntoView = jest.fn();
});

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
  transferableQty: 0,
  ...over,
});

const OP2 = row({ operationId: 2, operationCode: 'OP2' });
const ROWS = [
  row({ operationId: 1, operationCode: 'OP1' }),
  OP2,
  row({ operationId: 3, operationCode: 'OP3', groupName: 'Tay' }),
];

const setup = (rows: OutputGridRow[] = ROWS, requireReason = false) => {
  const onSave = jest.fn();
  const onClose = jest.fn();
  render(
    <WorkerOutputModal
      worker={{ workerId: 7, label: 'CN1 - An' }}
      rows={rows}
      saving={false}
      onSave={onSave}
      onClose={onClose}
      requireReason={requireReason}
    />,
  );
  return { onSave, onClose };
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

    fireEvent.click(screen.getByText(/Nhóm Tay/));
    expect(input('Sản lượng OP3').value).toBe('');
  });

  it('chỉ mở một nhóm mỗi lúc: mặc định nhóm đầu, mở nhóm khác thì nhóm đang mở tự đóng', () => {
    setup();
    expect(screen.queryByRole('spinbutton', { name: 'Sản lượng OP3' })).toBeNull();

    fireEvent.click(screen.getByText(/Nhóm Tay/));

    expect(screen.queryByRole('spinbutton', { name: 'Sản lượng OP1' })).toBeNull();
    expect(input('Sản lượng OP3')).toBeTruthy();
  });

  it('ô tìm công đoạn mở nhóm có công đoạn khớp', () => {
    setup();
    fireEvent.change(screen.getByPlaceholderText('Tìm mã, tên công đoạn'), {
      target: { value: 'op3' },
    });

    expect(input('Sản lượng OP3')).toBeTruthy();
    expect(screen.queryByText(/Nhóm Túi/)).toBeNull();
  });

  it('sửa lại từng công đoạn sau khi điền nhóm và lưu đúng số đã sửa', async () => {
    const { onSave, onClose } = setup();
    type('Sản lượng nhóm Túi', '20');
    type('Sản lượng OP2', '15');
    fireEvent.click(saveButton());

    expect(onSave).toHaveBeenCalledWith(
      [
        { operationId: 1, workerId: 7, quantity: 20, rowVer: null },
        { operationId: 2, workerId: 7, quantity: 15, rowVer: null },
      ],
      false,
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
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

  it('vượt số được giao nhưng chuyển được thì hỏi xác nhận rồi lưu kèm chuyển phần vượt', async () => {
    const { onSave, onClose } = setup([
      row({ operationId: 1, operationCode: 'OP1', capQty: 100, transferableQty: 30 }),
    ]);

    type('Sản lượng OP1', '120');
    expect(screen.getByText(/OP1: vượt 20/)).toBeInTheDocument();
    fireEvent.click(saveButton());

    expect(confirmDialog).toHaveBeenCalled();
    expect(onSave).toHaveBeenCalledWith(
      [{ operationId: 1, workerId: 7, quantity: 120, rowVer: null }],
      true,
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('vượt quá cả phần chuyển được thì báo lỗi và khóa nút lưu', () => {
    setup([row({ operationId: 1, operationCode: 'OP1', capQty: 100, transferableQty: 10 })]);

    type('Sản lượng OP1', '120');

    expect(screen.getByText(/chỉ chuyển được thêm 10/)).toBeInTheDocument();
    expect(saveButton()).toBeDisabled();
  });

  it('không có ô chọn thêm công đoạn; chưa được giao việc thì báo cần giao ở màn Giao việc', () => {
    setup([]);

    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.getByText(/chưa được giao việc nào ở dòng đơn này/)).toBeInTheDocument();
  });

  it('số 0 ở dòng đã nhập được gửi để xóa; ô để trống không gửi', async () => {
    const { onSave, onClose } = setup([
      row({ operationId: 1, operationCode: 'OP1', quantity: 10 }),
      OP2,
    ]);
    type('Sản lượng OP1', '0');
    fireEvent.click(saveButton());

    expect(onSave).toHaveBeenCalledWith(
      [{ operationId: 1, workerId: 7, quantity: 0, rowVer: null }],
      false,
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('tổ trưởng không có ô lý do sửa', () => {
    setup();

    expect(screen.queryByRole('textbox', { name: 'Lý do sửa (bắt buộc)' })).not.toBeInTheDocument();
  });

  it('Quản đốc sửa mà chưa ghi lý do thì báo và không lưu được; ghi lý do thì gửi kèm lý do', async () => {
    const { onSave, onClose } = setup(ROWS, true);
    type('Sản lượng OP1', '5');

    expect(saveButton()).toBeDisabled();
    expect(screen.getByText('Quản đốc sửa sản lượng phải ghi lý do.')).toBeInTheDocument();

    fireEvent.change(screen.getByRole('textbox', { name: 'Lý do sửa (bắt buộc)' }), {
      target: { value: 'Tổ báo thiếu' },
    });
    fireEvent.click(saveButton());

    expect(onSave).toHaveBeenCalledWith(
      [{ operationId: 1, workerId: 7, quantity: 5, rowVer: null }],
      false,
      'Tổ báo thiếu',
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('lưu lỗi thì không đóng popup', async () => {
    const onSave = jest.fn().mockRejectedValue(new Error('fail'));
    const onClose = jest.fn();
    render(
      <WorkerOutputModal
        worker={{ workerId: 7, label: 'CN1 - An' }}
        rows={ROWS}
        saving={false}
        onSave={onSave}
        onClose={onClose}
      />,
    );
    type('Sản lượng OP1', '5');
    fireEvent.click(saveButton());

    await waitFor(() => expect(onSave).toHaveBeenCalled());
    expect(onClose).not.toHaveBeenCalled();
  });
});
