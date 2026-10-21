using NX_MTD.API.Extensions;
using NX_MTD.API.Middleware;
using NX_MTD.API.Modules;
using NX_MTD.Application;
using NX_MTD.Infrastructure;
using NX_MTD.Infrastructure.Data;
using NX_MTD.Infrastructure.Modules;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, lc) => lc.MinimumLevel.Information().WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiCore(builder.Configuration);
builder.Services.AddSharedModule(builder.Configuration);
builder.Services.AddRequestModule();
builder.Services.AddOrderModule();
builder.Services.AddHrModule();

// ---- Modules: mỗi luồng thêm ĐÚNG 1 dòng của mình, mỗi module nằm trong file riêng ----
// builder.Services.AddProductionModule(builder.Configuration);   // Kiều
// builder.Services.AddWarehouseModule(builder.Configuration);    // Thái
builder.Services.AddAttendanceModule(builder.Configuration);      // Minh

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DbSeeder.SeedPasswordsAsync(app.Services);
}
app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
