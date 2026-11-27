# Báo cáo Kiểm thử & Nghiệm thu – CRM Mini Backend

- **Ngày thực hiện:** 2026-11-27
- **Môi trường kiểm thử tự động:** Windows (x64), `.NET SDK 10.0.404` (`Microsoft.AspNetCore.App 10.0.15`), xUnit + `WebApplicationFactory<Program>` + SQLite in-memory.
- **Lệnh kiểm thử:** `dotnet test CRMMini.sln --verbosity normal`

---

## 1. Tổng hợp Kết quả Test (`dotnet test`)

| Project | Tổng số test | Passed | Failed | Skipped | Thời gian |
|---|---:|---:|---:|---:|---:|
| `CRMMini.UnitTests` | 30 | 30 | 0 | 0 | 2.41s |
| `CRMMini.IntegrationTests` | 9 | 9 | 0 | 0 | 3.79s |
| **Tổng cộng** | **39** | **39** | **0** | **0** | **6.20s** |

- **Kết quả Build & Publish Release:** `dotnet build` và `dotnet publish src/CRMMini.API/CRMMini.API.csproj -c Release -o ./publish/api` thành công với **0 warning, 0 error**.

---

## 2. Phạm vi Kiểm thử Chi tiết

### 2.1 `CRMMini.UnitTests` (30 tests passed)
- **Domain Rules (`DomainRulesTests` – 20 test cases):**
  - `LeadStatusRules.CanTransition`: kiểm tra chuyển trạng thái hợp lệ (`New -> Contacted -> Qualified`, `Disqualified -> New`) và chặn chuyển trực tiếp sang/từ `Converted`.
  - `OpportunityStageRules.GetDefaultProbability`: xác suất mặc định cho 6 giai đoạn (`10%`, `25%`, `50%`, `75%`, `100%`, `0%`).
  - `OpportunityStageRules.CanTransition`: cho phép tiến/lùi các stage mở, đóng `ClosedWon/ClosedLost`, chặn `Staff` mở lại deal đã đóng, cho phép `Manager`/`Admin` mở lại về `Negotiation`.
- **Validators (`ValidatorTests` – 8 tests):**
  - Chính sách mật khẩu mạnh (`RegisterRequestValidator`).
  - Ràng buộc có ít nhất Email hoặc Số điện thoại cho `Customer`.
  - Giá trị âm ở `Lead.EstimatedValue`.
  - Bắt buộc `LossReason` khi chuyển `Opportunity` sang `ClosedLost`.
  - Bắt buộc `OpportunityTitle` khi `ConvertLeadRequest.CreateOpportunity = true`.
  - Ràng buộc `Activity` phải gắn ít nhất 1 target và `ReminderAt <= ScheduledAt`.
- **Auth Security (`AuthServiceUnitTests` – 2 tests):**
  - Tự động khóa tài khoản sau `MaxFailedLoginAttempts` lần đăng nhập sai và tự mở khóa sau hết thời gian cooldown.
  - Refresh Token rotation và thu hồi toàn bộ chuỗi token khi phát hiện reuse token đã thu hồi.

### 2.2 `CRMMini.IntegrationTests` (9 tests passed)
- **Health & Swagger/OpenAPI (`AuthAndRbacIntegrationTests`, `UsersTeamsAndSwaggerIntegrationTests`):**
  - `/health`, `/health/ready`, `/swagger/v1/swagger.json` trả `200 OK`; xác nhận OpenAPI schema chứa các route `/api/v1/...` và cấu hình bảo mật `Bearer`.
- **Auth & User/Team RBAC (`AuthAndRbacIntegrationTests`, `UsersTeamsAndSwaggerIntegrationTests`):**
  - Endpoint bảo vệ không có JWT trả `401 Unauthorized`.
  - Luồng Register -> `/users/me` -> chặn Staff tạo User (`403 Forbidden`) -> đổi mật khẩu (thu hồi refresh token cũ) -> login mật khẩu mới -> logout thu hồi refresh token.
  - Sai mật khẩu trả `401` RFC 9457 `ProblemDetails`; Admin khóa tài khoản (`IsLocked = true`) -> login bị chặn `403 Forbidden`.
  - Cập nhật profile `PUT /api/v1/users/me` lưu xuống DB thành công; phân quyền & data-scope cho `Teams` và `Users` (`Staff` bị `403`, `Manager` chỉ thấy Team của mình, `Admin` CRUD Team).
- **Company & Customer (`CustomerAndCompanyIntegrationTests`, `UsersTeamsAndSwaggerIntegrationTests`):**
  - CRUD Company & Customer, chặn trùng `TaxCode` và `Email` (`409 Conflict`).
  - Data-scope: `Staff` nhóm khác bị `403 Forbidden` và không thấy dữ liệu trong list; `Manager` cùng nhóm xem và xóa mềm được; `Staff` không được xóa Customer (`403`).
  - Lọc, sắp xếp (`sortBy=fullName&sortDir=desc`), phân trang (`page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`).
  - `GET /api/v1/customers/{id}/history` trả đầy đủ Opportunities, Activities và AuditLogs.
- **Lead, Opportunity Pipeline, Activity & Dashboard (`PipelineAndDashboardIntegrationTests`, `UsersTeamsAndSwaggerIntegrationTests`):**
  - Tạo Lead, chặn convert khi chưa `Qualified` (`422`), chặn `Manager` gán Lead sang `Staff` nhóm khác (`403`).
  - Chuyển `New -> Contacted -> Qualified` và Convert Lead thành Customer + Company + Opportunity trong 1 transaction; chặn double-convert (`409 Conflict`).
  - Chuyển `ClosedLost` thiếu `LossReason` trả `400`; `ClosedWon` tự gán `Probability = 100` và `ActualCloseDate`; `Staff` reopen trả `403`, `Manager` reopen sai stage trả `422`, reopen về `Negotiation` thành công (`200`).
  - Lịch sử Opportunity (`GET /api/v1/opportunities/{id}/history`), lọc Activity quá hạn (`overdueOnly=true`), tự động gán `CompletedAt` khi hoàn thành, chặn `Staff` khác xóa Activity.
  - Dashboard `summary`, `pipeline`, `activities` tính đúng KPI và giới hạn dữ liệu theo quyền người xem (`Admin` vs `Manager` vs `Staff`).

---

## 3. Các Lỗi Đã Phát hiện & Xử lý Trong Quá trình Kiểm thử

1. **EF Core 10 Dual Provider trong `WebApplicationFactory`:** Khi thay `UseSqlServer` bằng `UseSqlite` trong test, cần gỡ `IDbContextOptionsConfiguration<CrmDbContext>` bên cạnh `DbContextOptions<CrmDbContext>`. Đã sửa tại `tests/CRMMini.IntegrationTests/CrmWebAppFactory.cs`.
2. **Circular Dependency khi Seed Team & Manager cùng lúc:** Thêm `Team` + `User` (tham chiếu `TeamId`) + `Team.ManagerUserId` trong cùng 1 `SaveChangesAsync()` gây lỗi vòng phụ thuộc. Đã tách bước lưu `Team` & `User` trước rồi cập nhật `ManagerUserId`.
3. **Bug `.AsNoTracking()` khi ghi Profile & Đổi mật khẩu (`UserService`):** `UpdateMyProfileAsync` và `ChangeMyPasswordAsync` trước đó gọi helper đọc `LoadUserAsync` (có `.AsNoTracking()`), khiến thay đổi không lưu xuống DB khi kiểm thử lại bằng đăng nhập mới. Đã chuyển sang truy vấn có tracking trực tiếp trên `db.Users`.
4. **Trạng thái HTTP khi convert lại Lead đã chuyển đổi (`LeadService.ConvertAsync`):** Đã tách kiểm tra `lead.Status == LeadStatus.Converted` trả `409 Conflict` trước kiểm tra `!= Qualified` (`422 UnprocessableEntity`).

---

## 4. Kiểm chứng Thực tế trên SQL Server 2022 Express & Phạm vi Còn lại

- **Đã kiểm chứng thực tế trên SQL Server 2022 Express (`localhost\SQLEXPRESS` / `.\SQLEXPRESS`):**
  - Đã cấu hình User Secrets cục bộ cho `src/CRMMini.API` (không commit secret vào Git).
  - Đã chạy `dotnet dotnet-ef database update` tạo DB `CRMMiniDb` và áp dụng thành công migration `20261127032312_InitialCreate` trên `.\SQLEXPRESS`.
  - Đã chạy thực tế `CRMMini.API` (môi trường `Development`) kết nối `CRMMiniDb` trên SQL Server 2022 Express:
    - Tự động seed Roles, Team `SALES-HN`, 3 tài khoản mẫu (`admin@crmmini.local`, `manager@crmmini.local`, `staff@crmmini.local`) và dữ liệu demo.
    - `GET /health` và `GET /health/ready` trả `200 Healthy` (xác nhận kết nối SQL Server thật thành công).
    - `GET /swagger/v1/swagger.json` trả `200 OK`.
    - `GET /api/v1/customers` không có JWT trả `401 Unauthorized`.
    - `POST /api/v1/auth/login` (`staff@crmmini.local`) cấp JWT thành công; `GET /api/v1/customers` trả `totalCount = 1` (`Lê Minh Khách`); `GET /api/v1/dashboard/summary` trả `Customers = 1, Leads = 1, OpenOpps = 1, OpenPipeline = 250000000.00`.
- **Phạm vi chưa kiểm chứng trực tiếp:**
  - **IIS Live Server:** IIS (`W3SVC`) và `.NET 10 Hosting Bundle` chưa bật/cài trên máy hiện tại (cần chạy bằng quyền Administrator). Gói `Release` (`publish/api`) và `web.config` đã publish sẵn sàng theo [DEPLOYMENT_IIS.md](DEPLOYMENT_IIS.md).
