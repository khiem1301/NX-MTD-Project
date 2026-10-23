using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>
/// KH-24: phân quyền phía máy chủ. Mỗi controller phải nằm đúng thư mục vai trò và chỉ vai trò đó gọi được;
/// không có endpoint nào quên [Authorize]. Chạy bằng reflection nên chặn được controller mới thêm sai quyền.
/// </summary>
public class RbacControllerTests
{
    private static readonly Assembly Api = typeof(NX_MTD.API.Filters.AgentApiKeyAttribute).Assembly;

    private static readonly Dictionary<string, string> RolesByFolder = new()
    {
        ["Admin"] = Roles.Admin,
        ["Manager"] = Roles.Manager,
        ["Accountant"] = Roles.Accountant,
        ["WarehouseKeeper"] = Roles.WarehouseKeeper,
        ["TeamLeader"] = Roles.TeamLeaderOrDeputy,
        ["QaQc"] = Roles.QaQc,
        ["Electrician"] = Roles.Electrician,
        ["Worker"] = Roles.Worker,
    };

    private static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }
                                  && typeof(ControllerBase).IsAssignableFrom(t)
                                  && t.Namespace!.StartsWith("NX_MTD.API.Controllers."));

    private static string Folder(Type t) => t.Namespace!["NX_MTD.API.Controllers.".Length..];

    private static IEnumerable<AuthorizeAttribute> AuthorizeOf(Type t) =>
        t.GetCustomAttributes<AuthorizeAttribute>(inherit: true);

    public static IEnumerable<object[]> RoleControllers() =>
        Controllers().Where(t => RolesByFolder.ContainsKey(Folder(t))).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(RoleControllers))]
    public void Controller_chi_cho_dung_vai_tro_cua_thu_muc(Type controller)
    {
        var expected = RolesByFolder[Folder(controller)];
        var authorize = AuthorizeOf(controller).ToList();

        authorize.Should().NotBeEmpty($"{controller.Name} nằm ở thư mục {Folder(controller)} nên phải khóa theo vai trò");
        authorize.Should().OnlyContain(a => a.Roles == expected, $"{controller.Name} chỉ được mở cho {expected}");
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null,
                $"{controller.Name} không được có endpoint ẩn danh");
    }

    [Theory]
    [MemberData(nameof(RoleControllers))]
    public void Duong_dan_api_khop_thu_muc_vai_tro(Type controller)
    {
        var route = controller.GetCustomAttributes<RouteAttribute>(inherit: true).Select(r => r.Template).FirstOrDefault();
        route.Should().NotBeNull();
        var expectedSegment = Folder(controller) switch
        {
            "WarehouseKeeper" => "warehouse-keeper",
            "TeamLeader" => "team-leader",
            "QaQc" => "qa-qc",
            var f => f.ToLowerInvariant(),
        };
        route.Should().StartWith($"api/v1/{expectedSegment}/");
    }

    [Fact]
    public void Controller_dung_chung_deu_phai_dang_nhap_tru_dang_nhap_va_lam_moi_token()
    {
        var anonymousActions = new List<string>();
        foreach (var c in Controllers().Where(t => Folder(t) == "Common"))
        {
            var classLevel = AuthorizeOf(c).Any();
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttributes<HttpMethodAttribute>().Any() == false) continue;
                if (m.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                {
                    anonymousActions.Add($"{c.Name}.{m.Name}");
                    continue;
                }
                (classLevel || m.GetCustomAttribute<AuthorizeAttribute>() != null)
                    .Should().BeTrue($"{c.Name}.{m.Name} phải yêu cầu đăng nhập");
            }
        }

        anonymousActions.Should().BeEquivalentTo(new[] { "AuthController.Login", "AuthController.Refresh" });
    }

    [Fact]
    public void Chi_co_agent_cham_cong_dung_khoa_tinh_thay_dang_nhap()
    {
        var agent = Controllers().Where(t => Folder(t) == "Agent").ToList();
        agent.Should().ContainSingle();
        agent[0].GetCustomAttributes(inherit: true)
            .Should().Contain(a => a.GetType().Name == "AgentApiKeyAttribute");
    }

    [Fact]
    public void Khong_vai_tro_nao_vuot_quyen_Admin_ngoai_nhom_xu_ly_chung()
    {
        // Admin chỉ được vào các controller mang quyền Admin; không controller nào ngoài thư mục Admin cho riêng Admin.
        foreach (var c in Controllers().Where(t => Folder(t) != "Admin" && RolesByFolder.ContainsKey(Folder(t))))
            AuthorizeOf(c).Should().OnlyContain(a => a.Roles != Roles.Admin, c.Name);
    }
}
