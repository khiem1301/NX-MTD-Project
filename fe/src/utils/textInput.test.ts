import { CODE_PATTERN, normalizeCode, normalizePersonName, PERSON_NAME_PATTERN } from './textInput';

describe('normalizePersonName', () => {
  it('bỏ số và ký tự đặc biệt, giữ chữ có dấu', () => {
    expect(normalizePersonName('Nguyễn Văn A1@#')).toBe('Nguyễn Văn A');
  });

  it('gộp dấu cách thừa và bỏ dấu cách đầu', () => {
    expect(normalizePersonName('  Lê   Thị  Mai')).toBe('Lê Thị Mai');
  });

  it('bỏ dấu gạch, dấu chấm, dấu nháy', () => {
    expect(normalizePersonName("O'Connor-Smith.")).toBe('OConnorSmith');
  });

  it('giữ undefined', () => {
    expect(normalizePersonName(undefined)).toBeUndefined();
  });
});

describe('PERSON_NAME_PATTERN', () => {
  it.each(['Nguyễn Văn A', 'Trần', 'Lê Thị Mai '])('nhận %s', (name) => {
    expect(PERSON_NAME_PATTERN.test(name)).toBe(true);
  });

  it.each(['', ' ', 'Nguyễn Văn A1', 'An@', 'An-Bình', 'Nguyễn  Văn', '123'])(
    'từ chối "%s"',
    (name) => {
      expect(PERSON_NAME_PATTERN.test(name)).toBe(false);
    },
  );
});

describe('normalizeCode', () => {
  it('chỉ giữ chữ, số và . _ -', () => {
    expect(normalizeCode('AO 01/x_2.-é')).toBe('AO01x_2.-');
  });

  it('khớp CODE_PATTERN sau khi lọc', () => {
    expect(CODE_PATTERN.test(normalizeCode('ab c#1') ?? '')).toBe(true);
  });
});
