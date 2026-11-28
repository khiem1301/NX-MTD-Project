using Xunit.Abstractions;
using Xunit.Sdk;

namespace System.Tests;

// Chạy ca theo thứ tự mã cho các module có ca dùng tiếp trạng thái của ca trước
public sealed class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase =>
        testCases.OrderBy(t => t.TestMethod.Method.Name, StringComparer.Ordinal);
}
