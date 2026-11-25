using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>
/// ID động của bộ dữ liệu nền S0 (IT_B1 Phần 0). Test chỉ dùng các thuộc tính này — không hardcode ID.
/// Tên thuộc tính khớp alias trong Phần 0 (E_ADM → Adm, E_TL1 → Tl1, …); Account.Employee đã được nạp.
/// </summary>
public sealed class S0Context
{
    public const string Password = "P@ssw0rd!";

    // --- 16 nhân viên + tài khoản ---
    public Account Adm { get; set; } = null!;   // admin01
    public Account Adm2 { get; set; } = null!;  // admin02
    public Account Mgr { get; set; } = null!;   // mgr01
    public Account Acc { get; set; } = null!;   // acc01
    public Account Wh { get; set; } = null!;    // wh01
    public Account Tl1 { get; set; } = null!;   // tl01  — T_SEW01, 1101
    public Account Td1 { get; set; } = null!;   // td01  — T_SEW01, 1102
    public Account W1 { get; set; } = null!;    // wk01  — T_SEW01, 1001, B1
    public Account W2 { get; set; } = null!;    // wk02  — T_SEW02, 1002
    public Account W5 { get; set; } = null!;    // wk05  — T_SEW02, 1005
    public Account W3 { get; set; } = null!;    // wk03  — chưa có tổ
    public Account Tl2 { get; set; } = null!;   // tl02  — T_CUT01, 1201
    public Account W4 { get; set; } = null!;    // wk04  — T_CUT01, 1004
    public Account Rs { get; set; } = null!;    // rs01  — RESIGNED, 1009
    public Account Tmp { get; set; } = null!;   // tmp01 — QaQc, mật khẩu tạm
    public Account Lck { get; set; } = null!;   // lck01 — Electrician, Account LOCKED

    // --- 5 tổ ---
    public Team TSew01 { get; set; } = null!;
    public Team TSew02 { get; set; } = null!;
    public Team TCut01 { get; set; } = null!;
    public Team TEmpty { get; set; } = null!;
    public Team TOld { get; set; } = null!;

    // --- Bậc thợ ---
    public GradeLevelCode B1 { get; set; } = null!;
    public GradeLevelCode B2 { get; set; } = null!;
    public GradeLevelCode B3 { get; set; } = null!;
    public GradeLevelCode B9 { get; set; } = null!;
    public GradeLevel Gl1 { get; set; } = null!;
    public GradeLevel Gl2 { get; set; } = null!;
    public GradeLevel Gl3 { get; set; } = null!;

    // --- RJ snapshots ---
    public RjEmployeeSnapshot Rj2001 { get; set; } = null!;
    public RjEmployeeSnapshot Rj2002 { get; set; } = null!;
    public RjEmployeeSnapshot Rj1001 { get; set; } = null!;

    public IEnumerable<Account> AllAccounts =>
    [
        Adm, Adm2, Mgr, Acc, Wh, Tl1, Td1, W1, W2, W5, W3, Tl2, W4, Rs, Tmp, Lck,
    ];
}
