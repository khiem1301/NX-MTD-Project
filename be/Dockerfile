# syntax=docker/dockerfile:1
# Backend NX-MTD (.NET 9). Build context: thư mục be/

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore riêng để tận dụng cache layer khi chỉ đổi code
COPY NX-MTD.csproj ./
RUN dotnet restore NX-MTD.csproj

COPY . .
RUN dotnet publish NX-MTD.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Dùng image Debian (glibc), KHÔNG dùng alpine: Google.OrTools có thư viện native cho glibc
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Thư mục lưu file người dùng tải lên (ảnh đính kèm, bảng lương, nháp gợi ý bậc thợ).
# Gắn volume vào đây để không mất dữ liệu khi tạo lại container.
RUN mkdir -p /app/App_Data && chown -R app:app /app/App_Data

COPY --from=build /app/publish .

# Chạy bằng user không phải root (có sẵn trong image .NET 8+)
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NX-MTD.dll"]
