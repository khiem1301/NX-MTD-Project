using Microsoft.Playwright;

namespace System.Tests.Tools;

// Công cụ dò giao diện (không phải ca test): ST_EXPLORE="Role|/url|click1>>click2;..." ghi ARIA snapshot ra ST_EXPLORE_OUT.
public class Explore : BaseSystemTest
{
    [Fact]
    public async Task Dump()
    {
        var script = Environment.GetEnvironmentVariable("ST_EXPLORE");
        if (string.IsNullOrEmpty(script)) return;
        var sb = new System.Text.StringBuilder();
        foreach (var item in script.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Trim().Split('|');
            sb.AppendLine($"######## {item.Trim()}");
            try
            {
                var page = await NewPageAsRoleAsync(parts[0]);
                page.SetDefaultTimeout(8000);
                await GotoAndWaitAsync(page, parts[1]);
                if (parts.Length > 2)
                    foreach (var step in parts[2].Split(">>"))
                    {
                        var kv = step.Split('=', 2);
                        if (kv[0] == "choose")
                        {
                            var c = await page.RunAndWaitForFileChooserAsync(() => page.GetByText(kv[1]).First.ClickAsync());
                            await c.SetFilesAsync(Environment.GetEnvironmentVariable("ST_EXPLORE_FILE")!);
                            await page.WaitForResponseAsync(r => r.Url.Contains("excel") || r.Url.Contains("import"), new() { Timeout = 20000 });
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                            continue;
                        }
                        if (kv[0] == "dl")
                        {
                            var d = await page.RunAndWaitForDownloadAsync(() => page.GetByText(kv[1]).First.ClickAsync());
                            await d.SaveAsAsync(Environment.GetEnvironmentVariable("ST_EXPLORE_OUT") + ".xlsx");
                            continue;
                        }
                        var loc = kv[0] switch
                        {
                            "btn" => page.GetByRole(AriaRole.Button, new() { Name = kv[1] }).First,
                            "tab" => page.GetByRole(AriaRole.Tab, new() { Name = kv[1] }),
                            "label" => page.GetByLabel(kv[1]).First,
                            "text" => page.GetByText(kv[1]).First,
                            _ => page.Locator(kv[1]).First,
                        };
                        await loc.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await Expect(page.Locator(".ant-spin-spinning")).ToHaveCountAsync(0);
                        await Expect(page.GetByRole(AriaRole.Dialog).Or(page.Locator("main")).Last).ToBeVisibleAsync();
                    }
                var dialogs = page.GetByRole(AriaRole.Dialog);
                try { await dialogs.First.WaitForAsync(new() { Timeout = 4000 }); await page.WaitForLoadStateAsync(LoadState.NetworkIdle); await Expect(dialogs.Last).ToContainTextAsync(new System.Text.RegularExpressions.Regex(".{60,}", System.Text.RegularExpressions.RegexOptions.Singleline)); } catch { }
                var target = await dialogs.CountAsync() > 0 ? dialogs.Last : page.Locator("main, .ant-layout-content").First;
                sb.AppendLine(await target.AriaSnapshotAsync());
                var drop = page.Locator(".ant-select-dropdown:visible");
                if (await drop.CountAsync() > 0) sb.AppendLine("DROPDOWN: " + (await drop.Last.InnerTextAsync()).Replace('\n', ','));
            }
            catch (Exception ex) { sb.AppendLine("ERR " + ex.Message.Split('\n').Take(4).Aggregate((a, b) => a + " " + b)); }
        }
        await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("ST_EXPLORE_OUT")!, sb.ToString());
    }
}
