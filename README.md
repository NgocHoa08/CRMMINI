# CRM Mini Backend (.NET 10 / EF Core 10 / SQL Server)

Hệ thống Backend quản lý quan hệ khách hàng **CRM Mini (MVP)** xây dựng bằng **C# 14 / .NET 10 LTS**, **ASP.NET Core Controller-based Web API**, **Entity Framework Core 10**, và **SQL Server**.

---

## 1. Kiến trúc & Cấu trúc Solution

Giải pháp áp dụng **Clean / Layered Architecture** với hướng phụ thuộc chuẩn (`API` -> `Application` + `Infrastructure` -> `Domain`; `Infrastructure` -> `Application` -> `Domain`):

```text
CRMMini.sln
├── src/
│   ├── CRMMini.Domain/          # Entities, Enums, Rules (Pipeline/Lead), Domain Exceptions
│   ├── CRMMini.Application/     # DTOs, FluentValidation, Mappings, Data-Scope Helpers, Services
│   ├── CRMMini.Infrastructure/  # CrmDbContext, Fluent Configs, EF Migrations, Seed, JWT, Hash, Audit
│   └── CRMMini.API/             # Controllers v1, Auth/Policy, GlobalExceptionHandler, Swagger, HealthChecks, web.config
├── tests/
│   ├── CRMMini.UnitTests/       # Unit tests cho Domain rules, Validators, Auth lockout & token rotation
│   └── CRMMini.IntegrationTests/# End-to-end API tests qua WebApplicationFactory (RBAC, Data-Scope, CRUD, Pipeline, Dashboard)
├── docs/
│   ├── ERD.md
│   ├── API_CONTRACT.md
│   ├── DEPLOYMENT_IIS.md
│   ├── TEST_REPORT.md
│   └── database_migration.sql
├── postman/
│   ├── CRMMini.postman_collection.json
│   └── CRMMini.postman_environment.json
└── dotnet-tools.json            # Local tool manifest (dotnet-ef 10.0.15)
```

---

## 2. Module Nghiệp vụ Chính

- **User & Authentication:** Register, Login (`userName` hoặc `email`), JWT Access Token, Refresh Token rotation + phát hiện reuse, Logout, Profile, Đổi mật khẩu, tự động khóa tạm sau nhiều lần sai mật khẩu (`SecuritySettings`), Admin khóa/mở khóa và gán Role/Team, Audit Logs.
- **Team & Phân quyền (RBAC + Data-Scope):**
  - `Admin`: Toàn quyền hệ thống.
  - `Manager`: Xem & quản lý dữ liệu thuộc Team mình; phân công Lead/Opportunity trong Team; mở lại Opportunity đã đóng.
  - `Staff`: Thao tác trên dữ liệu mình sở hữu (`OwnerId`); xem danh mục Company cùng Team.
- **Company & Customer:** CRUD, tìm kiếm, lọc, sắp xếp, phân trang, kiểm tra trùng (`TaxCode`, `Email`, `Phone`), soft delete, API lịch sử chi tiết khách hàng (`GET /api/v1/customers/{id}/history`).
- **Lead:** CRUD, phân công (`assign`), state machine (`New -> Contacted -> Qualified -> Converted / Disqualified`), luồng chuyển đổi transaction sang Customer (+ tùy chọn Company & Opportunity).
- **Opportunity:** CRUD, pipeline 6 giai đoạn (`Qualification -> NeedsAnalysis -> Proposal -> Negotiation -> ClosedWon / ClosedLost`), quy tắc xác suất mặc định, bắt buộc `lossReason` khi `ClosedLost`, phân công, lịch sử thay đổi (`history`).
- **Activity:** Quản lý Call, Email, Meeting, Task, Reminder gắn với Customer/Lead/Opportunity, chuẩn hóa UTC/ISO-8601, lọc quá hạn (`overdueOnly`) và phân trang.
- **Dashboard:** Projection query tổng hợp KPI (`summary`), pipeline theo giai đoạn/nhân viên (`pipeline`), và hoạt động (`activities`) áp dụng chặt data-scope của người gọi.

---

## 3. Yêu cầu Môi trường & Cấu hình Secret

- **.NET SDK 10.0.x** (đã kiểm thử với `.NET SDK 10.0.404`)
- **SQL Server 2019+** / SQL Server Express / LocalDB (khi chạy ứng dụng thực tế)

> **Lưu ý bảo mật:** Không commit chuỗi kết nối thật, `JwtSettings:SecretKey` thật hoặc mật khẩu demo vào `appsettings.json`. Dùng **User Secrets** khi dev hoặc **Environment Variables** trên server.

Thiết lập User Secrets cho môi trường Development:
```powershell
dotnet user-secrets init --project src/CRMMini.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=CRMMiniDb;Trusted_Connection=True;TrustServerCertificate=True;" --project src/CRMMini.API
dotnet user-secrets set "JwtSettings:SecretKey" "<chuoi-bi-mat-toi-thieu-32-ky-tu>" --project src/CRMMini.API
dotnet user-secrets set "SeedData:DefaultDemoPassword" "<mat-khau-demo-manh>" --project src/CRMMini.API
```

---

## 4. Hướng dẫn Restore, Build, Migration, Run, Test & Publish

### 4.1 Restore & Build
```powershell
dotnet tool restore
dotnet restore CRMMini.sln
dotnet build CRMMini.sln
```

### 4.2 Database Migration (SQL Server)
Tạo hoặc cập nhật database SQL Server:
```powershell
dotnet dotnet-ef database update --project src/CRMMini.Infrastructure --startup-project src/CRMMini.API
```
Hoặc dùng file SQL idempotent đã xuất sẵn tại [docs/database_migration.sql](docs/database_migration.sql).

### 4.3 Chạy Ứng dụng & Mở Swagger
```powershell
dotnet run --project src/CRMMini.API
```
Sau khi chạy (môi trường `Development` bật sẵn Swagger):
- Swagger UI: `https://localhost:7075/swagger` (hoặc `http://localhost:5127/swagger`)
- OpenAPI JSON: `/swagger/v1/swagger.json`
- Health Checks: `/health` và `/health/ready`

Nếu bật `SeedData:Enabled = true` và đặt `SeedData:DefaultDemoPassword`, hệ thống tự seed sẵn:
- Team `SALES-HN`
- Tài khoản mẫu: `admin@crmmini.local` (`Admin`), `manager@crmmini.local` (`Manager`), `staff@crmmini.local` (`Staff`)
- Dữ liệu mẫu Company, Customer, Lead, Opportunity và Activity.

### 4.4 Chạy Kiểm thử Tự động
```powershell
dotnet test CRMMini.sln
```
(Xem chi tiết kết quả kiểm thử thực tế tại [docs/TEST_REPORT.md](docs/TEST_REPORT.md).)

### 4.5 Publish Release (IIS)
```powershell
dotnet publish src/CRMMini.API/CRMMini.API.csproj -c Release -o ./publish/api
```
Xem hướng dẫn cấu hình IIS, .NET 10 Hosting Bundle, HTTPS, quyền thư mục, backup/restore SQL Server và rollback tại [docs/DEPLOYMENT_IIS.md](docs/DEPLOYMENT_IIS.md).

---

## 5. Tài liệu Tham khảo & Bộ Giao tiếp

- Thiết kế dữ liệu & ERD: [docs/ERD.md](docs/ERD.md)
- Tài liệu API Contract & Problem Details: [docs/API_CONTRACT.md](docs/API_CONTRACT.md)
- Hướng dẫn deploy IIS & Backup/Rollback: [docs/DEPLOYMENT_IIS.md](docs/DEPLOYMENT_IIS.md)
- Báo cáo kiểm thử: [docs/TEST_REPORT.md](docs/TEST_REPORT.md)
- Postman Collection & Environment mẫu: [postman/CRMMini.postman_collection.json](postman/CRMMini.postman_collection.json), [postman/CRMMini.postman_environment.json](postman/CRMMini.postman_environment.json)
