USE NXMTD;
GO
SET NOCOUNT ON;

-- Tổ may mẫu (script chính chỉ có VP và CAT)
IF NOT EXISTS (SELECT 1 FROM dbo.Team WHERE TeamCode = 'MAY1')
    INSERT dbo.Team (TeamCode, TeamName, TeamType) VALUES ('MAY1', N'Tổ may 1', 'SEWING');

DECLARE @vp   INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP');
DECLARE @cat  INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT');
DECLARE @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');

DECLARE @seed TABLE (Code VARCHAR(20), FullName NVARCHAR(100), RoleCode VARCHAR(30), TeamId INT, PayType VARCHAR(20));
INSERT @seed (Code, FullName, RoleCode, TeamId, PayType) VALUES
 ('QUANDOC',  N'Quản đốc mẫu',        'MANAGER',          @vp,   'MONTHLY'),
 ('KETOAN',   N'Kế toán mẫu',         'ACCOUNTANT',       @vp,   'MONTHLY'),
 ('THUKHO',   N'Thủ kho mẫu',         'WAREHOUSE_KEEPER', @vp,   'MONTHLY'),
 ('TTCAT',    N'Tổ trưởng tổ cắt',    'TEAM_LEADER',      @cat,  'MONTHLY'),
 ('TTMAY',    N'Tổ trưởng tổ may 1',  'TEAM_LEADER',      @may1, 'MONTHLY'),
 ('TPMAY',    N'Tổ phó tổ may 1',     'TEAM_DEPUTY',      @may1, 'MONTHLY'),
 ('QAQC1',    N'QA/QC mẫu',           'QAQC',             @vp,   'DAILY'),
 ('CODIEN',   N'Cơ điện mẫu',         'ELECTRICIAN',      @vp,   'MONTHLY'),
 ('CNMAY1',   N'Công nhân may 1',     'WORKER',           @may1, 'PIECE_RATE'),
 ('CNCAT1',   N'Công nhân cắt 1',     'WORKER',           @cat,  'DAILY');

INSERT dbo.Employee (EmployeeCode, FullName, RoleId, TeamId, PayType, StartDate)
SELECT s.Code, s.FullName, r.RoleId, s.TeamId, s.PayType, '2026-01-01'
FROM @seed s JOIN dbo.Role r ON r.RoleCode = s.RoleCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.EmployeeCode = s.Code);

INSERT dbo.Account (EmployeeId, Username, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, e.EmployeeCode, N'__SET_BY_BACKEND_SEED__', 1
FROM dbo.Employee e
WHERE e.EmployeeCode IN (SELECT Code FROM @seed)
  AND NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

SELECT a.Username, r.RoleCode, t.TeamCode
FROM dbo.Account a JOIN dbo.Employee e ON e.EmployeeId = a.EmployeeId
JOIN dbo.Role r ON r.RoleId = e.RoleId LEFT JOIN dbo.Team t ON t.TeamId = e.TeamId
ORDER BY a.AccountId;
GO
