/* =====================================================================================
   01. TỔ, NHÂN VIÊN, TÀI KHOẢN, BẬC THỢ, KỲ KẾ TOÁN
   Tên đăng nhập = mã nhân viên. Mật khẩu: tài khoản cũ giữ mật khẩu đang dùng; tài khoản mới dùng mật khẩu mặc định (123456),
   không bắt đổi mật khẩu để đăng nhập test ngay.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);

INSERT dbo.Team (TeamCode, TeamName, TeamType, Status) VALUES
 ('VP',   N'Khối văn phòng', 'OFFICE',  'ACTIVE'),
 ('CAT',  N'Tổ cắt',         'CUTTING', 'ACTIVE'),
 ('MAY1', N'Tổ may 1',       'SEWING',  'ACTIVE'),
 ('MAY2', N'Tổ may 2',       'SEWING',  'ACTIVE'),
 ('MAY3', N'Tổ may 3',       'SEWING',  'ACTIVE'),
 ('MAY0', N'Tổ may cũ (đã giải thể)', 'SEWING', 'DISCONTINUED');

-- Code | Tên | Giới | Ngày sinh | SĐT | Role | Tổ | PayType | Bậc | Mã chấm công | Vào làm | Trạng thái
DECLARE @e TABLE (Code VARCHAR(20), FullName NVARCHAR(100), Gender CHAR(1), Dob DATE, Phone VARCHAR(15), RoleCode VARCHAR(30),
                  TeamCode VARCHAR(20), PayType VARCHAR(20), Grade VARCHAR(5), Att INT, Start DATE, Resign DATE);
INSERT @e VALUES
 -- Khối văn phòng
 ('ADMIN',   N'Trần Quốc Hưng',     'M', '1974-03-12', '0903111001', 'ADMIN',            'VP',   'MONTHLY', NULL, 1,   '2015-01-05', NULL),
 ('QUANDOC', N'Lê Minh Tuấn',       'M', '1981-07-25', '0903111002', 'MANAGER',          'VP',   'MONTHLY', NULL, 2,   '2016-03-01', NULL),
 ('KETOAN',  N'Phạm Thị Hương',     'F', '1986-11-02', '0903111003', 'ACCOUNTANT',       'VP',   'MONTHLY', NULL, 3,   '2017-06-12', NULL),
 ('THUKHO',  N'Đỗ Văn Thắng',       'M', '1984-05-18', '0903111004', 'WAREHOUSE_KEEPER', 'VP',   'MONTHLY', NULL, 4,   '2018-02-19', NULL),
 ('QAQC1',   N'Nguyễn Thị Lụa',     'F', '1990-09-09', '0903111005', 'QAQC',             'VP',   'DAILY',   NULL, 5,   '2019-04-08', NULL),
 ('QAQC2',   N'Bùi Văn Cường',      'M', '1992-01-30', '0903111006', 'QAQC',             'VP',   'DAILY',   NULL, 6,   '2021-08-16', NULL),
 ('CODIEN',  N'Vũ Đình Công',       'M', '1988-12-14', '0903111007', 'ELECTRICIAN',      'VP',   'MONTHLY', NULL, 7,   '2018-10-01', NULL),
 -- Tổ cắt
 ('TTCAT',   N'Hoàng Văn Sơn',      'M', '1983-04-04', '0903222001', 'TEAM_LEADER',      'CAT',  'MONTHLY', NULL, 11,  '2016-05-09', NULL),
 ('TPCAT',   N'Lương Thị Bích',     'F', '1989-08-21', '0903222002', 'TEAM_DEPUTY',      'CAT',  'MONTHLY', NULL, 12,  '2019-01-14', NULL),
 ('CNCAT1',  N'Đặng Văn Tài',       'M', '1991-02-11', '0903222011', 'WORKER',           'CAT',  'DAILY',   NULL, 13,  '2020-03-02', NULL),
 ('CNCAT2',  N'Mai Văn Quý',        'M', '1993-06-27', '0903222012', 'WORKER',           'CAT',  'DAILY',   NULL, 14,  '2021-05-17', NULL),
 ('CNCAT3',  N'Trịnh Văn Phát',     'M', '1995-10-05', '0903222013', 'WORKER',           'CAT',  'DAILY',   NULL, 15,  '2022-02-21', NULL),
 ('CNCAT4',  N'Lê Thị Thanh',       'F', '1994-03-19', '0903222014', 'WORKER',           'CAT',  'DAILY',   NULL, 16,  '2022-09-05', NULL),
 ('CNCAT5',  N'Võ Văn Lộc',         'M', '1996-12-23', '0903222015', 'WORKER',           'CAT',  'DAILY',   NULL, 17,  '2023-04-10', NULL),
 ('CNCAT6',  N'Tạ Văn Hiếu',        'M', '1997-07-07', '0903222016', 'WORKER',           'CAT',  'DAILY',   NULL, 18,  '2025-06-02', NULL),
 -- Tổ may 1
 ('TTMAY',   N'Nguyễn Thị Hồng',    'F', '1985-01-16', '0903333001', 'TEAM_LEADER',      'MAY1', 'MONTHLY', 'A',  21,  '2016-08-22', NULL),
 ('TPMAY',   N'Trần Thị Mai',       'F', '1990-05-29', '0903333002', 'TEAM_DEPUTY',      'MAY1', 'MONTHLY', 'A',  22,  '2019-07-01', NULL),
 ('CNMAY1',  N'Nguyễn Thị Lan',     'F', '1992-04-03', '0903333011', 'WORKER',           'MAY1', 'PIECE_RATE', 'B', 23, '2019-11-04', NULL),
 ('CNMAY2',  N'Phạm Thị Hoa',       'F', '1994-08-15', '0903333012', 'WORKER',           'MAY1', 'PIECE_RATE', 'A', 24, '2018-03-12', NULL),
 ('CNMAY3',  N'Trần Văn Hùng',      'M', '1989-11-28', '0903333013', 'WORKER',           'MAY1', 'PIECE_RATE', 'A', 25, '2017-09-18', NULL),
 ('CNMAY4',  N'Lê Thị Mai',         'F', '1996-02-06', '0903333014', 'WORKER',           'MAY1', 'PIECE_RATE', 'C', 26, '2022-01-10', NULL),
 ('CNMAY5',  N'Vũ Thị Thu',         'F', '1993-09-17', '0903333015', 'WORKER',           'MAY1', 'PIECE_RATE', 'B', 27, '2020-06-08', NULL),
 ('CNMAY6',  N'Đỗ Văn Nam',         'M', '1991-12-01', '0903333016', 'WORKER',           'MAY1', 'PIECE_RATE', 'B', 28, '2020-10-19', NULL),
 ('CNMAY7',  N'Hoàng Thị Yến',      'F', '1997-05-24', '0903333017', 'WORKER',           'MAY1', 'PIECE_RATE', 'C', 29, '2023-02-13', NULL),
 ('CNMAY8',  N'Bùi Thị Ngọc',       'F', '1998-10-10', '0903333018', 'WORKER',           'MAY1', 'PIECE_RATE', 'D', 30, '2025-09-15', NULL),
 -- Tổ may 2
 ('TTMAY2',  N'Lê Văn Đức',         'M', '1984-06-08', '0903444001', 'TEAM_LEADER',      'MAY2', 'MONTHLY', 'A',  31,  '2016-11-07', NULL),
 ('TPMAY2',  N'Ngô Thị Hạnh',       'F', '1991-03-13', '0903444002', 'TEAM_DEPUTY',      'MAY2', 'MONTHLY', 'B',  32,  '2019-09-23', NULL),
 ('CNMAY9',  N'Dương Thị Ánh',      'F', '1993-01-21', '0903444011', 'WORKER',           'MAY2', 'PIECE_RATE', 'A', 33, '2018-05-14', NULL),
 ('CNMAY10', N'Hồ Văn Khoa',        'M', '1990-07-30', '0903444012', 'WORKER',           'MAY2', 'PIECE_RATE', 'B', 34, '2019-02-25', NULL),
 ('CNMAY11', N'Mai Thị Dung',       'F', '1995-04-12', '0903444013', 'WORKER',           'MAY2', 'PIECE_RATE', 'B', 35, '2021-03-29', NULL),
 ('CNMAY12', N'Cao Văn Phúc',       'M', '1992-10-26', '0903444014', 'WORKER',           'MAY2', 'PIECE_RATE', 'C', 36, '2021-11-08', NULL),
 ('CNMAY13', N'Đinh Thị Hiền',      'F', '1996-06-18', '0903444015', 'WORKER',           'MAY2', 'PIECE_RATE', 'C', 37, '2022-07-04', NULL),
 ('CNMAY14', N'Tô Văn Đức',         'M', '1994-12-09', '0903444016', 'WORKER',           'MAY2', 'PIECE_RATE', 'B', 38, '2020-12-14', NULL),
 ('CNMAY15', N'La Thị Nga',         'F', '1997-02-02', '0903444017', 'WORKER',           'MAY2', 'PIECE_RATE', 'D', 39, '2024-05-20', NULL),
 ('CNMAY16', N'Kiều Văn Tín',       'M', '1999-09-14', '0903444018', 'WORKER',           'MAY2', 'PIECE_RATE', 'D', 40, '2025-10-06', NULL),
 -- Tổ may 3 (mới, chưa có tổ phó)
 ('TTMAY3',  N'Phan Thị Lan',       'F', '1987-08-05', '0903555001', 'TEAM_LEADER',      'MAY3', 'MONTHLY', 'B',  41,  '2021-01-18', NULL),
 ('CNMAY17', N'Lưu Văn Quân',       'M', '1995-03-22', '0903555011', 'WORKER',           'MAY3', 'PIECE_RATE', 'C', 42, '2023-08-07', NULL),
 ('CNMAY18', N'Trịnh Thị Oanh',     'F', '1996-11-11', '0903555012', 'WORKER',           'MAY3', 'PIECE_RATE', 'C', 43, '2023-08-07', NULL),
 ('CNMAY19', N'Vương Văn Hải',      'M', '1998-01-27', '0903555013', 'WORKER',           'MAY3', 'PIECE_RATE', 'D', 44, '2025-07-21', NULL),
 -- Chưa map mã chấm công (để test màn map mã) và chưa vào tổ
 ('CNMAY20', N'Phan Thị Trang',     'F', '1999-05-03', '0903555014', 'WORKER',           'MAY3', 'PIECE_RATE', 'D', NULL, DATEADD(DAY, -9, @today), NULL),
 ('CNMAY21', N'Nguyễn Văn Bảo',     'M', '2000-08-19', '0903555015', 'WORKER',           'MAY3', 'PIECE_RATE', 'D', NULL, DATEADD(DAY, -9, @today), NULL),
 ('CNMOI1',  N'Lý Thị Kim Chi',     'F', '2001-02-14', '0903555016', 'WORKER',           NULL,   'PIECE_RATE', NULL, NULL, DATEADD(DAY, -2, @today), NULL),
 -- Đã nghỉ việc
 ('NVNGHI1', N'Lý Văn Sơn',         'M', '1990-04-25', '0903666001', 'WORKER',           'MAY2', 'PIECE_RATE', 'C', 91, '2019-01-07', DATEADD(DAY, -40, @today)),
 ('NVNGHI2', N'Tạ Thị Yến',         'F', '1988-09-02', '0903666002', 'WORKER',           'CAT',  'DAILY',   NULL, 92, '2018-04-23', DATEADD(DAY, -75, @today));

INSERT dbo.Employee (EmployeeCode, FullName, Gender, DateOfBirth, Phone, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode,
                     StartDate, ResignDate, Status)
SELECT e.Code, e.FullName, e.Gender, e.Dob, e.Phone, r.RoleId, t.TeamId, e.PayType, e.Grade, e.Att, e.Start, e.Resign,
       IIF(e.Resign IS NULL, 'ACTIVE', 'RESIGNED')
FROM @e e
JOIN dbo.Role r ON r.RoleCode = e.RoleCode
LEFT JOIN dbo.Team t ON t.TeamCode = e.TeamCode;

-- Tài khoản: giữ hash cũ theo Username, nhân viên mới dùng hash mật khẩu mặc định
INSERT dbo.Account (EmployeeId, Username, PasswordHash, IsTemporaryPassword, Status, LockedAt, LockReason)
SELECT em.EmployeeId, em.EmployeeCode,
       COALESCE(o.PasswordHash, (SELECT TOP 1 PasswordHash FROM #DefaultHash)),
       IIF(o.PasswordHash IS NULL AND (SELECT TOP 1 PasswordHash FROM #DefaultHash) = N'__SET_BY_BACKEND_SEED__', 1, 0),
       IIF(em.Status = 'RESIGNED', 'LOCKED', 'ACTIVE'),
       IIF(em.Status = 'RESIGNED', DATEADD(DAY, 1, CAST(em.ResignDate AS DATETIME2)), NULL),
       IIF(em.Status = 'RESIGNED', N'Nhân viên đã nghỉ việc', NULL)
FROM dbo.Employee em
LEFT JOIN #OldHash o ON o.Username = em.EmployeeCode;

/* ---- Bậc thợ: đề xuất ở đủ ba trạng thái + lịch sử đã áp dụng ---- */
DECLARE @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @ttmay2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY2'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @may2 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY2'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC');
INSERT dbo.GradeEvaluation (WorkerId, ProposedBy, TeamId, CurrentGradeCode, ProposedGradeCode, Reason, RecentDefectCount,
                            EvaluationDate, Status, EffectiveDate, ConsideredAt, DecisionNote, CreatedAt)
SELECT w.EmployeeId, p.pb, p.tid, p.cur, p.prop, p.reason, p.defects, DATEADD(DAY, p.evalOff, @today), p.st,
       CASE WHEN p.st = 'APPLIED' THEN DATEADD(DAY, p.evalOff + 5, @today) END,
       CASE WHEN p.st IN ('APPLIED', 'NOT_APPLIED') THEN DATEADD(DAY, p.evalOff + 3, CAST(@today AS DATETIME2)) END,
       p.note, DATEADD(DAY, p.evalOff, CAST(@today AS DATETIME2))
FROM (VALUES
 ('CNMAY4',  @ttmay,  @may1, 'C', 'B', N'Tay nghề ổn định 3 tháng, sản lượng vượt định mức',   0, -4,   'SUBMITTED',   NULL),
 ('CNMAY8',  @ttmay,  @may1, 'D', 'C', N'Học nhanh, ít lỗi sau 2 tháng',                        1, -6,   'SUBMITTED',   NULL),
 ('CNMAY12', @ttmay2, @may2, 'C', 'B', N'Làm đều các công đoạn khó',                            0, -30,  'APPLIED',     N'Đồng ý nâng bậc'),
 ('CNMAY13', @ttmay2, @may2, 'C', 'B', N'Đề xuất nâng bậc',                                     3, -33,  'NOT_APPLIED', N'Còn nhiều lỗi đường may, chưa nâng'),
 ('CNMAY9',  @ttmay2, @may2, 'B', 'A', N'Thợ giỏi, nhận được công đoạn khó nhất',              0, -80,  'APPLIED',     N'Đồng ý nâng bậc A')
) p (Code, pb, tid, cur, prop, reason, defects, evalOff, st, note)
JOIN dbo.Employee w ON w.EmployeeCode = p.Code;

/* ---- Kỳ kế toán 2026: bổ sung nếu thiếu (khóa sau khi có dữ liệu ở bước 99) ---- */
;WITH m AS (SELECT 1 AS n UNION ALL SELECT n + 1 FROM m WHERE n < 12)
INSERT dbo.AccountingPeriod (PeriodMonth, StartDate, EndDate)
SELECT CONCAT('2026-', RIGHT(CONCAT('0', n), 2)), DATEFROMPARTS(2026, n, 1), EOMONTH(DATEFROMPARTS(2026, n, 1))
FROM m
WHERE NOT EXISTS (SELECT 1 FROM dbo.AccountingPeriod p WHERE p.PeriodMonth = CONCAT('2026-', RIGHT(CONCAT('0', n), 2)));

PRINT N'01: tổ, nhân viên, tài khoản xong.';
