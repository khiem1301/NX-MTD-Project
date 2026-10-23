using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Application.Interfaces;

namespace Application.Tests;

/// <summary>
/// KH-24 / R1 / GBR-02: phạm vi dữ liệu 3 tầng (Công khai / Vận hành / Mật), kiểm bằng cấu trúc controller.
/// - Mật: tổng hợp chi phí và hòa vốn chỉ Admin; doanh thu và đơn giá công đoạn chỉ Admin + Kế toán.
/// - Vận hành: công nhân và tổ trưởng không tự chọn người / tổ để xem — danh tính lấy từ token.
/// Phần lọc theo tổ / theo người bên trong dịch vụ đã có test riêng ở từng *ServiceTests.
/// </summary>
public class DataScopeTests
{
    private static readonly Assembly Api = typeof(NX_MTD.API.Filters.AgentApiKeyAttribute).Assembly;

    private static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }
                                  && typeof(ControllerBase).IsAssignableFrom(t)
                                  && t.Namespace!.StartsWith("NX_MTD.API.Controllers."));

    private static string Folder(Type t) => t.Namespace!["NX_MTD.API.Controllers.".Length..];

    /// <summary>Thư mục (vai trò) được phép inject dịch vụ chứa dữ liệu tầng Mật.</summary>
    private static readonly Dictionary<Type, string[]> ConfidentialServices = new()
    {
        [typeof(ICostSummaryService)] = new[] { "Admin" },                         // tổng hợp chi phí: chỉ Admin
        [typeof(IFinanceService)] = new[] { "Admin", "Accountant" },               // doanh thu + hòa vốn
        [typeof(IPieceRateService)] = new[] { "Admin", "Accountant" },             // đơn giá công đoạn (CMT)
    };

    [Fact]
    public void Confidential_services_are_only_injected_into_allowed_role_folders()
    {
        var violations = new List<string>();
        foreach (var controller in Controllers())
        {
            var injected = controller.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
            foreach (var service in injected.Where(ConfidentialServices.ContainsKey))
                if (!ConfidentialServices[service].Contains(Folder(controller)))
                    violations.Add($"{Folder(controller)}/{controller.Name} dùng {service.Name}");

            // Cũng tính dịch vụ nhận qua [FromServices] trên action.
            foreach (var service in controller.GetMethods().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType)
                         .Where(ConfidentialServices.ContainsKey))
                if (!ConfidentialServices[service].Contains(Folder(controller)))
                    violations.Add($"{Folder(controller)}/{controller.Name} nhận {service.Name} qua action");
        }

        violations.Should().BeEmpty("dữ liệu tầng Mật không được lộ sang vai trò khác");
    }

    [Fact]
    public void Break_even_and_cost_summary_exist_only_for_admin()
    {
        foreach (var name in new[] { "BreakEvenController", "CostSummaryController" })
            Controllers().Where(c => c.Name == name).Select(Folder).Should().BeEquivalentTo(new[] { "Admin" }, name);
    }

    [Fact]
    public void Revenue_exists_only_for_admin_and_accountant()
    {
        Controllers().Where(c => c.Name == "RevenueController").Select(Folder)
            .Should().BeEquivalentTo(new[] { "Admin", "Accountant" });
    }

    [Fact]
    public void Break_even_endpoints_are_not_reachable_from_the_accountant_folder()
    {
        // Kế toán chỉ có Revenue của IFinanceService; hòa vốn nằm ở BreakEvenController (Admin).
        var accountantRevenue = Controllers().Single(c => c.Name == "RevenueController" && Folder(c) == "Accountant");
        accountantRevenue.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Should().NotContain(n => n.Contains("BreakEven", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] IdentityParameterNames = { "employeeid", "workerid", "userid", "accountid" };

    [Fact]
    public void Worker_endpoints_never_take_the_person_from_the_client()
    {
        var self = Controllers().Where(c => Folder(c) == "Worker"
                                            || (Folder(c) == "Common" && c.Name.StartsWith("My", StringComparison.Ordinal)));
        var violations = (from c in self
                          from m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                          from p in m.GetParameters()
                          where IdentityParameterNames.Contains(p.Name!.ToLowerInvariant())
                          select $"{c.Name}.{m.Name}({p.Name})").ToList();

        violations.Should().BeEmpty("công nhân chỉ xem dữ liệu của chính mình (R1)");
    }

    [Fact]
    public void Team_leader_endpoints_never_take_the_team_from_the_route_or_query_string()
    {
        var violations = (from c in Controllers().Where(c => Folder(c) == "TeamLeader")
                          from m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                          from p in m.GetParameters()
                          where string.Equals(p.Name, "teamId", StringComparison.OrdinalIgnoreCase)
                          select $"{c.Name}.{m.Name}({p.Name})").ToList();

        violations.Should().BeEmpty("tổ trưởng / tổ phó chỉ làm việc trong tổ của mình, lấy từ token");
    }

    [Fact]
    public void Manager_cannot_reach_admin_only_hr_actions()
    {
        // GBR-01: Quản đốc chỉ có cập nhật giới hạn; tạo, nghỉ việc và đặt lại mật khẩu nằm ở thư mục Admin.
        var manager = Controllers().Single(c => c.Name == "EmployeesController" && Folder(c) == "Manager");
        manager.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Should().NotContain(new[] { "Create", "Resign", "ResetPassword", "AdminUpdate" });
    }
}
