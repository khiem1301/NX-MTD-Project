import { PARTNER_LIMITS, partnerRules } from './partnerRules';

type Rule = { validator: (rule: unknown, value: unknown) => Promise<void> };
const ok = async (rule: unknown, value: unknown) =>
  (rule as Rule).validator({}, value).then(
    () => true,
    () => false,
  );
const message = async (rule: unknown, value: unknown) =>
  (rule as Rule).validator({}, value).then(
    () => '',
    (e: Error) => e.message,
  );
const pad = (n: number, c = 'x') => c.repeat(n);

describe('partnerRules', () => {
  const name = partnerRules.required('Tên khách hàng', PARTNER_LIMITS.name);

  it('tên bắt buộc: trống, toàn khoảng trắng hoặc undefined đều bị báo', async () => {
    expect(await ok(name, '')).toBe(false);
    expect(await ok(name, '   ')).toBe(false);
    expect(await ok(name, undefined)).toBe(false);
    expect(await message(name, '')).toBe('Vui lòng nhập thông tin này.');
  });

  it('tên: đúng 200 ký tự thì qua, 201 thì lỗi kèm câu tiếng Việt', async () => {
    expect(await ok(name, pad(200))).toBe(true);
    expect(await ok(name, pad(201))).toBe(false);
    expect(await message(name, pad(201))).toBe('Tên khách hàng tối đa 200 ký tự.');
  });

  it('độ dài tính sau khi cắt khoảng trắng đầu cuối (khớp BE)', async () => {
    expect(await ok(name, ` ${pad(200)} `)).toBe(true);
    expect(await ok(name, ` ${pad(201)} `)).toBe(false);
  });

  it('thông báo bắt buộc tùy biến được', async () => {
    const custom = partnerRules.required('Tên', 200, 'Vui lòng nhập tên nhà cung cấp.');
    expect(await message(custom, '')).toBe('Vui lòng nhập tên nhà cung cấp.');
  });

  it('trường không bắt buộc: trống được, vượt giới hạn thì lỗi', async () => {
    const note = partnerRules.optional('Ghi chú', PARTNER_LIMITS.note);
    expect(await ok(note, '')).toBe(true);
    expect(await ok(note, undefined)).toBe(true);
    expect(await ok(note, '   ')).toBe(true);
    expect(await ok(note, pad(500))).toBe(true);
    expect(await ok(note, pad(501))).toBe(false);
  });

  it.each([
    ['0901234567', true],
    ['+84901234567', true],
    ['090123456', false],
    ['09012345678', false],
    ['+840901234567', false],
    ['090 123 4567', false],
    ['09012345ab', false],
    ['', false],
  ])('số điện thoại %s -> %s', async (phone, expected) => {
    expect(await ok(partnerRules.phone, phone)).toBe(expected);
  });

  it.each([
    ['a@b.vn', true],
    ['ten.ho+nhan@mail.example.com', true],
    [' a@b.vn ', true],
    ['a@b', false],
    ['a b@c.vn', false],
    ['a@@b.vn', false],
    ['a@b..vn', false],
    ['nguyễn@b.vn', false],
    ['a@bé.vn', false],
    ['', false],
  ])('email %s -> %s', async (email, expected) => {
    expect(await ok(partnerRules.email, email)).toBe(expected);
  });

  it('email: đúng 100 ký tự thì qua, 101 thì lỗi', async () => {
    const email = (n: number) => `${pad(n - 6, 'a')}@b.com`;
    expect(await ok(partnerRules.email, email(100))).toBe(true);
    expect(await ok(partnerRules.email, email(101))).toBe(false);
    expect(await message(partnerRules.email, email(101))).toBe('Email tối đa 100 ký tự.');
  });
});
