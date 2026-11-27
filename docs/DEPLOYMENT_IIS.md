# Hướng dẫn Triển khai Windows Server / IIS & Database Backup/Rollback

Tài liệu phục vụ triển khai **CRM Mini Backend (.NET 10)** lên môi trường kiểm thử/staging Windows Server + IIS + SQL Server.

---

## 1. Yêu cầu Tiên quyết trên Windows Server
1. Cài đặt **ASP.NET Core Runtime 10.0.x & .NET 10 Hosting Bundle** (tải từ trang chính thức Microsoft .NET 10 Downloads).
2. Sau khi cài Hosting Bundle, khởi động lại IIS bằng lệnh (Run as Administrator):
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. Kiểm tra module `AspNetCoreModuleV2` đã đăng ký trong IIS Manager -> Server -> Modules.
4. Cài đặt **SQL Server** (hoặc dùng SQL Server từ xa đã cấu hình TCP/IP & tài khoản SQL có quyền `db_owner` hoặc tối thiểu quyền chạy migration/CRUD).

---

## 2. Publish Ứng dụng Cấu hình Release

Trên máy build/CI:
```powershell
dotnet publish src/CRMMini.API/CRMMini.API.csproj -c Release -o ./publish/api
```
Copy toàn bộ thư mục `./publish/api` lên thư mục deploy trên server (ví dụ `C:\inetpub\crmmini-api`). Kiểm tra đã có file `web.config` sử dụng `AspNetCoreModuleV2`.

> **Tùy chọn tự động hóa (Local/Staging):** Có thể chạy script [scripts/Deploy-IIS.ps1](../scripts/Deploy-IIS.ps1) bằng quyền Administrator để tự động copy `publish/api`, tạo AppPool `CRMMiniApiPool`, tạo Site `CRMMiniAPI` (cổng `5080`), cấp quyền NTFS + SQL Server cho `IIS APPPOOL\CRMMiniApiPool` và kiểm tra `/health/ready`:
> ```bat
> powershell -NoProfile -ExecutionPolicy Bypass -File "W:\CRM Mini\scripts\Deploy-IIS.ps1"
> ```

---

## 3. Cấu hình IIS Site, Application Pool & Quyền Thư mục

1. **Tạo Application Pool:**
   - Name: `CRMMiniApiPool`
   - `.NET CLR version`: **No Managed Code** (bắt buộc cho .NET Core/.NET 10 out-of-process hoặc in-process).
   - Managed pipeline mode: `Integrated`.
2. **Tạo Website / Application:**
   - Physical path: `C:\inetpub\crmmini-api`
   - Application pool: `CRMMiniApiPool`
   - Binding: **HTTPS** (port `443` hoặc cổng staging kèm chứng chỉ SSL/TLS hợp lệ).
3. **Cấp quyền thư mục (NTFS Permissions):**
   - Tạo thư mục `C:\inetpub\crmmini-api\logs` (nếu bật `stdoutLogEnabled="true"` khi debug sự cố khởi động).
   - Cấp quyền **Read & Execute** trên `C:\inetpub\crmmini-api` và **Modify** trên `C:\inetpub\crmmini-api\logs` cho tài khoản `IIS AppPool\CRMMiniApiPool`.

---

## 4. Quản lý Secrets & Environment Variables (Không commit secret)

Cấu hình các biến môi trường ở cấp **IIS Application Pool / Web.config environmentVariables (chỉ cấu hình tại server, không commit)** hoặc **Windows System Environment Variables**:

| Biến môi trường | Ý nghĩa |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` hoặc `Production` |
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối SQL Server thật |
| `JwtSettings__SecretKey` | Chuỗi bí mật ký JWT (tối thiểu 32 ký tự ngẫu nhiên) |
| `JwtSettings__Issuer` | Tên Issuer (ví dụ `CRMMini.Staging`) |
| `JwtSettings__Audience` | Tên Audience |
| `Database__InitializeOnStartup` | `true` (seed 3 role mặc định nếu chưa có) |
| `Database__AutoMigrateOnStartup` | `false` (khuyến nghị chạy migration chủ động bằng SQL script/CLI trước khi start) |
| `SeedData__Enabled` | `false` trên Production (`true` chỉ dùng ở môi trường Demo/QA nếu cần) |
| `Swagger__Enabled` | `true` ở Staging/Kiểm thử, `false` ở Production |

---

## 5. Chuẩn bị & Chạy Migration SQL Server

Từ máy build/CI:
```powershell
dotnet dotnet-ef migrations script --idempotent --project src/CRMMini.Infrastructure --startup-project src/CRMMini.Infrastructure --output docs/database_migration.sql
```
Áp dụng lên SQL Server đích trước khi bật bản publish mới:
- Hoặc chạy file `docs/database_migration.sql` qua SSMS / `sqlcmd`.
- Hoặc chạy trực tiếp:
  ```powershell
  $env:ConnectionStrings__DefaultConnection = "<SQL_SERVER_CONNECTION_STRING>"
  dotnet dotnet-ef database update --project src/CRMMini.Infrastructure --startup-project src/CRMMini.Infrastructure
  ```

---

## 6. Backup & Khôi phục SQL Server (Demo / Kiểm thử)

**Backup trước mỗi lần deploy hoặc trước buổi demo:**
```sql
BACKUP DATABASE [CRMMiniDb]
TO DISK = N'D:\SQLBackups\CRMMiniDb_PreDeploy.bak'
WITH INIT, COMPRESSION, STATS = 10;
```

**Khôi phục (Restore) khi cần rollback dữ liệu:**
```sql
USE [master];
ALTER DATABASE [CRMMiniDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [CRMMiniDb]
FROM DISK = N'D:\SQLBackups\CRMMiniDb_PreDeploy.bak'
WITH REPLACE, STATS = 10;
ALTER DATABASE [CRMMiniDb] SET MULTI_USER;
```

---

## 7. Checklist Triển khai & Kiểm chứng

- [ ] Đã backup database `CRMMiniDb` trước khi nâng cấp.
- [ ] Đã chạy script migration SQL Server (`docs/database_migration.sql`) không lỗi.
- [ ] Đã cài `.NET 10 Hosting Bundle` và tạo AppPool `No Managed Code`.
- [ ] Đã thiết lập đủ biến môi trường `ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`.
- [ ] Đã bật HTTPS binding với chứng chỉ hợp lệ.
- [ ] Kiểm tra `GET /health` trả `200 Healthy` và `GET /health/ready` trả `200 Healthy` (xác nhận kết nối SQL Server thành công).
- [ ] Kiểm tra đăng nhập `POST /api/v1/auth/login` và gọi thử endpoint bảo vệ `GET /api/v1/customers`.
- [ ] Nếu bật tạm `stdoutLogEnabled="true"` trong `web.config` để debug, nhớ tắt lại `false` sau khi ổn định.

---

## 8. Quy trình Rollback

1. Dừng IIS Website / AppPool `CRMMiniApiPool` (`Stop-WebAppPool -Name "CRMMiniApiPool"`).
2. Khôi phục thư mục code phiên bản trước (đã nén backup trước khi copy bản mới).
3. Nếu bản deploy mới có thay đổi schema gây không tương thích ngược, thực hiện **Restore Database** từ bản backup `.bak` ở Mục 6.
4. Khởi động lại AppPool (`Start-WebAppPool -Name "CRMMiniApiPool"`) và kiểm tra `GET /health/ready`.
