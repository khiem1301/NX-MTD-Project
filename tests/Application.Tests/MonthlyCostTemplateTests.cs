using FluentAssertions;
using NX_MTD.Infrastructure.Services;

namespace Application.Tests;

/// <summary>File mẫu chi phí tháng: dòng ví dụ phải nằm ngoài sheet nhập để không bị nhập nhầm thành khoản chi 0 đồng.</summary>
[Collection(ExcelTestCollection.Name)]
public class MonthlyCostTemplateTests
{
    [Fact]
    public void Template_ImportSheetHasNoDataRows_SoNothingIsImportedByAccident()
    {
        var template = new ClosedXmlMonthlyCostWriter().WriteTemplate();

        var rows = new ClosedXmlMonthlyCostParser().Parse(new MemoryStream(template));

        rows.Should().BeEmpty();
    }
}
