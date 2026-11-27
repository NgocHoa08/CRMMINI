# CRM Mini – Đặc tả yêu cầu & Master Prompt

> Bản tổng hợp từ giáo án .NET 10; các phần đề xuất được đánh dấu rõ để phân biệt với yêu cầu nguồn.

## 1. Mục tiêu dự án

Xây dựng CRM Mini – hệ thống backend quản lý quan hệ khách hàng, có thể tích hợp Front-end và triển khai môi trường kiểm thử. Phạm vi chính là MVP theo Giai đoạn 4 (Ngày 22–30) trong giáo án .NET 10 được cung cấp.

Sản phẩm cuối: CRM Mini Backend hoàn chỉnh ở phạm vi MVP, sẵn sàng tích hợp Front-end. Tài liệu gốc không mô tả giao diện Front-end cụ thể; vì vậy tài liệu này tập trung vào Backend/API và chỉ nêu yêu cầu giao tiếp để Front-end tích hợp.

## 2. Phạm vi module bắt buộc

Các module được nêu trực tiếp trong giáo án: User, Customer, Company, Lead, Opportunity, Activity; Dashboard được triển khai ở Ngày 27.

2.1 User & Authentication
- Quản lý User, Role và quan hệ.
- Register, Login, Profile, đổi mật khẩu.
- Password hashing; JWT và refresh token theo thiết kế.
- Role/Policy; kiểm soát tài khoản bị khóa.
- Ghi audit các hoạt động đăng nhập quan trọng.
- User API và Authentication Service; kiểm thử token, role/claim, đăng nhập thành công/thất bại và tài khoản bị khóa.

2.2 Customer
- CRUD khách hàng.
- Tìm kiếm, lọc, sắp xếp, phân trang.
- DTO, validation, mapping.
- Kiểm tra dữ liệu trùng theo quy tắc nghiệp vụ.
- Phân quyền theo người phụ trách/nhóm.
- Audit và soft delete khi cần.
- API chi tiết khách hàng kèm lịch sử liên quan.

2.3 Company
- CRUD doanh nghiệp.
- DTO, validation, mapping.
- Hỗ trợ liên kết doanh nghiệp với khách hàng theo mô hình dữ liệu đã thiết kế.

2.4 Lead
- Quản lý khách hàng tiềm năng.
- Thông tin nguồn, trạng thái, người phụ trách và liên hệ.
- Phân công người phụ trách.
- Kiểm tra dữ liệu nghiệp vụ.
- Có luồng chuyển đổi Lead sang Customer theo đặc tả nghiệp vụ được thống nhất.

2.5 Opportunity
- Quản lý cơ hội kinh doanh.
- Theo dõi giá trị, giai đoạn, xác suất và ngày dự kiến.
- Pipeline và quy tắc chuyển trạng thái.
- Lịch sử thay đổi và phân công.
- Thống kê theo giai đoạn/người phụ trách.

2.6 Activity
- Quản lý cuộc gọi, email, cuộc họp, công việc và nhắc lịch.
- Liên kết Activity với Customer, Lead hoặc Opportunity.
- Xử lý ngày giờ/timezone và phân trang.

2.7 Dashboard
- Cung cấp dữ liệu tổng quan về khách hàng, Lead, Opportunity và Activity.
- Truy vấn tổng hợp/projection.
- Áp dụng quyền xem dữ liệu.
- Kiểm thử truy vấn tổng hợp và quyền truy cập.

## 3. Yêu cầu kiến trúc và công nghệ

Công nghệ theo giáo án:
- C# 14, .NET 10 LTS.
- ASP.NET Core Web API; ưu tiên Controller-based Web API.
- Entity Framework Core 10.
- SQL Server.
- JWT authentication; Role/Policy authorization.
- OpenAPI/Swagger; Postman.
- xUnit hoặc framework test .NET phù hợp; dotnet test.
- Git; triển khai Windows Server/IIS ở môi trường kiểm thử.

Kiến trúc:
- Tổ chức theo Layered Architecture/Clean Architecture.
- Tách Domain, Application, Infrastructure, API.
- Tuân thủ Dependency Rule; project reference đúng hướng.
- Có DTO, mapping, interface/implementation và dependency injection.
- Không lạm dụng abstraction/project nếu không tạo giá trị.

Solution tham khảo trong tài liệu:
CRMMini.sln
├── CRMMini.Domain
│   ├── Entities
│   ├── Enums
│   └── Interfaces
├── CRMMini.Application
│   ├── DTOs
│   ├── Features
│   ├── Interfaces
│   ├── Services
│   └── Validators
├── CRMMini.Infrastructure
│   ├── Persistence
│   ├── Configurations
│   ├── Repositories
│   └── Services
└── CRMMini.API
    ├── Controllers
    ├── Middleware
    ├── Extensions
    ├── Program.cs
    └── appsettings.json

Lưu ý phiên bản: EF Core 10 yêu cầu .NET 10; chọn phiên bản Microsoft.EntityFrameworkCore và SQL Server Provider tương ứng.

## 4. Yêu cầu phi chức năng

Các yêu cầu được nêu trong giáo án: bảo mật, logging, hiệu năng, backup; validation và Problem Details thống nhất; Health Check; kiểm soát quyền dữ liệu; quản lý secret/cấu hình môi trường.

Yêu cầu triển khai:
- Publish cấu hình Release.
- Cài .NET 10 Hosting Bundle phù hợp mô hình hosting.
- Cấu hình IIS site, application pool và quyền thư mục.
- Cấu hình connection string, environment variables và secret management.
- Bật HTTPS; kiểm tra log, health check và xử lý lỗi triển khai.
- Không commit secret hoặc cấu hình nhạy cảm.
- Có checklist triển khai/rollback.
- Có backup database và hướng dẫn khôi phục phù hợp môi trường demo/kiểm thử.

Yêu cầu API:
- Route nhất quán; giáo án minh họa /api/v1/customers.
- Pagination, filtering, sorting; response contract thống nhất.
- API versioning, tương thích ngược và rate limiting.
- OpenAPI mô tả authentication và ví dụ request.
- Dùng status code đúng; lỗi trả về Problem Details nhất quán.

## 5. Phân quyền

Giáo án yêu cầu role/policy và kiểm soát quyền dữ liệu theo user/team, nhưng không ấn định tên role hoặc ma trận chi tiết. Khi triển khai cần tạo ma trận quyền được chủ dự án duyệt trước khi code.

Đề xuất ma trận khởi đầu (cần xác nhận):
- Admin: quản trị tài khoản, role, cấu hình và xem/quản lý dữ liệu toàn hệ thống.
- Manager: xem dữ liệu nhóm; phân công; theo dõi hiệu suất và pipeline nhóm.
- Staff: quản lý dữ liệu được giao/phụ trách; không tự cấp quyền hoặc xem dữ liệu ngoài phạm vi.

Quy tắc bắt buộc: kiểm tra quyền ở Backend/API, không chỉ ẩn nút trên Front-end. Test cả truy cập hợp lệ và truy cập bị từ chối.

## 6. Luồng nghiệp vụ cần hỗ trợ

Luồng tổng quát để thiết kế use case và demo:
1. Người dùng đăng nhập; API cấp token.
2. Nhân viên tạo Lead từ nguồn tiếp cận và gán người phụ trách.
3. Nhân viên cập nhật trạng thái, ghi Activity chăm sóc.
4. Lead đủ điều kiện được chuyển đổi thành Customer (và Company nếu có).
5. Tạo Opportunity liên quan; cập nhật giá trị, xác suất, ngày dự kiến và giai đoạn pipeline.
6. Ghi nhận cuộc gọi/email/cuộc họp/công việc dưới dạng Activity.
7. Opportunity kết thúc theo trạng thái thắng/thua hoặc trạng thái nghiệp vụ được thống nhất.
8. Dashboard tổng hợp dữ liệu theo quyền của người xem.

Các trạng thái cụ thể, điều kiện chuyển trạng thái và quy tắc chuyển đổi Lead phải được mô tả trong đặc tả nghiệp vụ; không tự ý giả định khi chưa được xác nhận.

## 7. Thiết kế dữ liệu – yêu cầu và đề xuất

Giáo án yêu cầu có ERD, khóa và quan hệ rõ ràng, migration tạo/cập nhật database. Giáo án không cung cấp ERD hoặc danh sách cột chính thức.

Các thực thể tối thiểu suy ra từ module: User, Role, Customer, Company, Lead, Opportunity, Activity. Có thể cần bảng liên kết User-Role và các bảng phục vụ refresh token/audit nếu thiết kế sử dụng.

Đề xuất kỹ thuật (không phải ERD chính thức của giáo án):
- Users, Roles, UserRoles
- Customers, Companies
- Leads, Opportunities, Activities
- RefreshTokens, AuditLogs

Khi thiết kế ERD cần xác định PK/FK, nullability, unique constraints, index, quy tắc xóa, created/updated timestamps, người tạo/người cập nhật và quan hệ lịch sử. Chỉ thêm bảng/field khi có lý do nghiệp vụ. Phải bàn giao ERD và migration chạy được trên SQL Server.

## 8. API contract – danh sách endpoint gợi ý

Giáo án yêu cầu lập danh sách endpoint theo module, API contract và Postman Collection nhưng không quy định endpoint cụ thể. Danh sách sau là khung đề xuất để AI/dev hoàn thiện, không phải endpoint bắt buộc nguyên văn:

Auth/User:
- POST /api/v1/auth/register
- POST /api/v1/auth/login
- POST /api/v1/auth/refresh
- POST /api/v1/auth/logout
- GET  /api/v1/users/me
- PUT  /api/v1/users/me
- PUT  /api/v1/users/me/password
- GET/POST/PUT /api/v1/users (theo quyền quản trị)

Customer/Company:
- GET/POST /api/v1/customers
- GET/PUT/DELETE /api/v1/customers/{id}
- GET /api/v1/customers/{id}/history
- GET/POST /api/v1/companies
- GET/PUT/DELETE /api/v1/companies/{id}

Lead/Opportunity:
- GET/POST /api/v1/leads
- GET/PUT/DELETE /api/v1/leads/{id}
- POST /api/v1/leads/{id}/assign
- POST /api/v1/leads/{id}/convert
- GET/POST /api/v1/opportunities
- GET/PUT/DELETE /api/v1/opportunities/{id}
- POST /api/v1/opportunities/{id}/stage
- POST /api/v1/opportunities/{id}/assign

Activity/Dashboard:
- GET/POST /api/v1/activities
- GET/PUT/DELETE /api/v1/activities/{id}
- GET /api/v1/dashboard/summary
- GET /api/v1/dashboard/pipeline
- GET /api/v1/dashboard/activities

Quy tắc chung: endpoint cần request/response DTO, validation, status code, authorization policy, filter/sort/page khi phù hợp, ví dụ lỗi và mô tả OpenAPI.

## 9. Kiểm thử và nghiệm thu

Kiểm thử:
- Unit Test cho service và validator.
- Integration Test cho endpoint trọng yếu.
- Arrange–Act–Assert; test tích cực và tiêu cực.
- Test authentication, authorization, validation và xử lý lỗi.
- Test đăng nhập thành công/thất bại, tài khoản bị khóa.
- Test CRUD, lọc, phân trang và quyền dữ liệu theo user/team.
- Test chuyển trạng thái pipeline hợp lệ/không hợp lệ.
- Test truy vấn Dashboard và giới hạn dữ liệu theo quyền.
- Chạy toàn bộ test bằng dotnet test; nộp báo cáo test và danh sách lỗi đã xử lý.

Tiêu chí đánh giá trong giáo án:
- Kiến thức và giải thích kỹ thuật: 20%.
- Thực hành và chất lượng mã nguồn: 30%.
- Bài tập và module nghiệp vụ: 30%.
- Kiểm thử, demo và trình bày: 20%.

## 10. Checklist bàn giao bắt buộc

☐ Solution và source code được quản lý bằng Git.
☐ Database CRM Mini có ERD, khóa và quan hệ rõ ràng.
☐ Migration tạo/cập nhật database theo hướng dẫn.
☐ API User và Authentication hoạt động.
☐ API Customer/Company có CRUD, lọc và phân trang.
☐ API Lead/Opportunity có pipeline cơ bản.
☐ API Activity/Dashboard có dữ liệu tổng hợp.
☐ JWT, role/policy và kiểm soát quyền dữ liệu.
☐ Validation và Problem Details thống nhất.
☐ Logging và Health Check hoạt động.
☐ OpenAPI/Swagger và Postman Collection được bàn giao.
☐ Unit Test và Integration Test cho luồng cốt lõi.
☐ Ứng dụng publish và chạy trên IIS môi trường kiểm thử.
☐ README, cấu hình môi trường và hướng dẫn deploy đầy đủ.

Bộ bàn giao nên gồm: source code, database script/migrations, ERD, API documentation/OpenAPI, Postman Collection + Environment mẫu không chứa secret, README build/run/migration, hướng dẫn deploy/rollback và báo cáo test.

## 11. Công nghệ mở rộng – không bắt buộc cho MVP

Giáo án liệt kê AutoMapper, FluentValidation, Serilog, Redis, Hangfire, SignalR, MediatR, xUnit, Docker, YARP để làm quen. Redis, Hangfire, SignalR, RabbitMQ, Docker, CI/CD, Azure, CQRS, Microservices và API Gateway là nội dung nâng cao/mở rộng; không đưa vào điều kiện nghiệm thu MVP nếu chưa có yêu cầu riêng. Ưu tiên hoàn thành nghiệp vụ cốt lõi, bảo mật, test và triển khai trước.

## 12. Kịch bản demo tối thiểu

1. Chạy ứng dụng và mở Swagger.
2. Đăng nhập bằng tài khoản demo; nhận JWT.
3. Thử endpoint không token và chứng minh bị từ chối.
4. Tạo Company và Customer; tìm kiếm/lọc/phân trang.
5. Tạo Lead, gán người phụ trách, cập nhật trạng thái và ghi Activity.
6. Chuyển đổi Lead; tạo Opportunity; cập nhật pipeline.
7. Tạo Activity gắn với Customer/Lead/Opportunity.
8. Mở Dashboard; kiểm tra thống kê và quyền xem.
9. Chạy dotnet test.
10. Publish và xác nhận API chạy trên IIS môi trường kiểm thử.

## 13. Những điểm cần chốt trước khi code

Các điểm tài liệu nguồn chưa quy định chi tiết, cần người phụ trách dự án xác nhận:
- Tên role chính thức và ma trận quyền cuối cùng.
- Bộ trạng thái Lead, Opportunity và quy tắc chuyển trạng thái.
- Quy tắc trùng Customer/Company/Lead (email, số điện thoại, mã số thuế...).
- Trường dữ liệu bắt buộc cho từng entity.
- Quy tắc chuyển đổi Lead thành Customer/Company và xử lý dữ liệu trùng.
- Có cần nhiều nhóm/chi nhánh hay chỉ một team.
- Quy tắc soft delete, lưu lịch sử và thời hạn lưu audit.
- Dashboard cần KPI/biểu đồ cụ thể nào.
- Cơ chế refresh/revoke token, thời hạn token và chính sách mật khẩu.
- Cấu hình môi trường SQL Server/IIS thực tế.
Không tự bịa các quyết định này; ghi rõ giả định và xin xác nhận trước khi triển khai nếu ảnh hưởng dữ liệu/nghiệp vụ.

## 14. MASTER PROMPT – giao cho AI lập trình

Bạn là Senior .NET Backend Engineer/Software Architect. Hãy xây dựng một dự án CRM Mini Backend theo đúng tài liệu yêu cầu trong file này. Hãy đọc toàn bộ tài liệu trước khi code.

MỤC TIÊU
Tạo CRM Mini Backend MVP bằng C# 14, .NET 10, ASP.NET Core Web API, EF Core 10 và SQL Server; có thể tích hợp Front-end và deploy lên Windows Server/IIS.

PHẠM VI
Bắt buộc có các module User/Authentication, Customer, Company, Lead, Opportunity, Activity và Dashboard. Thực hiện đầy đủ CRUD/nghiệp vụ, tìm kiếm/lọc/sắp xếp/phân trang ở nơi phù hợp, validation, phân quyền dữ liệu, logging, health check, OpenAPI/Swagger, Postman, unit/integration test và tài liệu triển khai theo các mục trong tài liệu.

KIẾN TRÚC
Tạo solution CRMMini.sln gồm CRMMini.Domain, CRMMini.Application, CRMMini.Infrastructure, CRMMini.API và test project phù hợp. Áp dụng Clean/Layered Architecture vừa đủ, đúng chiều phụ thuộc; dùng DTO, service, interface, DI, EF Core migrations. Ưu tiên Controller-based API. Không tạo abstraction thừa.

NGHIỆP VỤ
Triển khai User/Role/Auth; Customer/Company; Lead; Opportunity với pipeline; Activity; Dashboard. Các trạng thái, quy tắc chuyển đổi, trường dữ liệu và quyền chưa được chốt phải được liệt kê thành câu hỏi/giả định trước khi triển khai, không tự suy diễn âm thầm.

BẢO MẬT
Dùng password hashing an toàn; JWT và refresh token theo thiết kế; role/policy authorization; kiểm soát tài khoản bị khóa; bảo vệ secret; không trả password/hash/token nhạy cảm trong API; kiểm tra quyền ở server; có audit cho hoạt động quan trọng. Không commit connection string/secret thật.

API
Dùng route versioned nhất quán (ví dụ /api/v1); chuẩn hóa response, status code, validation và Problem Details; hỗ trợ pagination/filter/sort; cấu hình rate limiting phù hợp; cung cấp OpenAPI/Swagger và Postman Collection/Environment mẫu không chứa secret.

DATABASE
Thiết kế ERD trước khi code; xác định PK/FK, unique/index, quan hệ và quy tắc xóa; tạo EF Core migrations; cung cấp seed data demo an toàn. Không tự thêm bảng hoặc field không có mục đích nghiệp vụ; giải thích các đề xuất schema.

TEST
Viết Unit Test cho service/validator và Integration Test cho endpoint cốt lõi; test happy path, negative path, auth, role/policy, validation, CRUD, pipeline, dashboard và data-scope. Chạy dotnet test và sửa lỗi trước khi bàn giao.

DEPLOY
Cung cấp README hướng dẫn restore/build/run/migration/seed/test/publish; cấu hình environment variables/secrets; hướng dẫn IIS, .NET 10 Hosting Bundle, HTTPS, quyền thư mục, log, health check và rollback. Không giả định đã deploy thành công nếu chưa chạy kiểm chứng.

QUY TRÌNH LÀM VIỆC
1. Đọc yêu cầu, liệt kê phạm vi và các điểm chưa rõ.
2. Đề xuất use case, ERD, ma trận quyền, API contract và solution tree để review.
3. Sau khi chốt các điểm nghiệp vụ quan trọng, tạo solution và database.
4. Triển khai theo module, mỗi module có DTO, validation, authorization và test.
5. Tích hợp, chạy migration, build và test; sửa toàn bộ lỗi phát hiện được.
6. Tạo Swagger, Postman, README, hướng dẫn deploy và checklist nghiệm thu.
7. Cuối mỗi giai đoạn báo cáo file đã tạo/sửa, lệnh đã chạy, kết quả build/test và việc còn thiếu.

NGUYÊN TẮC
- Không chỉ tạo code minh họa hoặc pseudo-code; tạo source code có thể build/run.
- Không bỏ trống TODO ở chức năng thuộc MVP.
- Không bịa kết quả test/deploy.
- Không hard-code secret.
- Không thay đổi phạm vi hoặc công nghệ cốt lõi nếu chưa giải thích và xin xác nhận.
- Ưu tiên code dễ đọc, maintainable, đúng nghiệp vụ và bảo mật.
- Nếu thiếu thông tin, hỏi từng nhóm câu hỏi quan trọng hoặc ghi giả định rõ ràng trước khi tiếp tục.
- Hoàn tất với checklist bàn giao ở Mục 10 của tài liệu.

## 15. Nguồn và giới hạn của tài liệu

Tài liệu được biên soạn dựa trên file “Giao_an_DotNet_10_30_ngay.pdf”, tập trung Giai đoạn 4 – CRM Mini (Ngày 22–30), cấu trúc Solution CRM Mini tham khảo, checklist sản phẩm cuối khóa, đánh giá/nghiệm thu và lưu ý phiên bản/phạm vi.

Phân biệt:
- “Yêu cầu theo giáo án”: nội dung được nêu trực tiếp trong nguồn.
- “Đề xuất triển khai”: ví dụ role Admin/Manager/Staff, danh sách endpoint, tên bảng dữ liệu, luồng demo; đây là cách cụ thể hóa để giao cho AI/dev, cần review và xác nhận.
- Tài liệu gốc không có ERD CRM hoàn chỉnh, wireframe UI, use case diagram hay ảnh giao diện CRM. Phụ lục dưới đây đính kèm ảnh chụp nguyên trang PDF nguồn có nội dung CRM liên quan; không giả mạo chúng thành thiết kế UI/ERD.

