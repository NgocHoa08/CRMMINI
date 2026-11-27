# API Contract – CRM Mini Backend (`/api/v1`)

- **Base URL mẫu (Local HTTP/HTTPS):** `https://localhost:7075` hoặc `http://localhost:5127`
- **Swagger UI:** `/swagger`
- **OpenAPI JSON:** `/swagger/v1/swagger.json`
- **Health Checks:** `GET /health` (liveness), `GET /health/ready` (readiness – kiểm tra SQL Server)
- **Authentication:** JWT Bearer (`Authorization: Bearer <accessToken>`)
- **Pagination Response:** `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0, "hasPreviousPage": false, "hasNextPage": false }`
- **Error Response:** RFC 9457 `ProblemDetails` (`application/problem+json`) kèm `errorCode` và `traceId`.

---

## 1. Ma trận Phân quyền Tóm tắt

| Tài nguyên | Admin | Manager | Staff |
|---|---|---|---|
| Users / Teams | Quản trị toàn hệ thống | Xem danh sách user & team thuộc nhóm mình | Chỉ `GET/PUT /api/v1/users/me` và đổi mật khẩu |
| Companies | Toàn quyền CRUD | CRUD trong phạm vi Team | Xem trong Team, tạo & sửa công ty mình phụ trách; không xóa |
| Customers | Toàn quyền CRUD | CRUD trong phạm vi Team | Xem, tạo, sửa Customer mình sở hữu (`OwnerId`); không xóa |
| Leads | Toàn quyền CRUD, Assign, Convert | CRUD, Assign, Convert trong phạm vi Team | Xem, tạo, sửa, Convert Lead của mình; không assign sang người khác; không xóa |
| Opportunities | Toàn quyền CRUD, Stage (kể cả reopen), Assign | CRUD, Stage (kể cả reopen), Assign trong Team | CRUD, đổi Stage các Opportunity mở của mình; không reopen deal đã đóng; không assign/xóa |
| Activities | Toàn quyền CRUD | CRUD trong phạm vi Team | CRUD Activity của mình gắn với bản ghi được quyền truy cập |
| Dashboard | Toàn hệ thống | Tổng hợp theo Team | Tổng hợp dữ liệu cá nhân |

---

## 2. Danh sách Endpoint Chi tiết

### 2.1 Auth (`/api/v1/auth`, Rate limit policy: `auth`)
| Method | Route | Auth | Mô tả | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Anonymous | Đăng ký tài khoản mới (role mặc định `Staff`) | `201`, `400`, `409`, `429` |
| `POST` | `/api/v1/auth/login` | Anonymous | Đăng nhập bằng `userNameOrEmail` + `password` | `200`, `400`, `401`, `403`, `429` |
| `POST` | `/api/v1/auth/refresh` | Anonymous | Cấp lại JWT + rotate Refresh Token (chống reuse) | `200`, `400`, `401`, `403`, `429` |
| `POST` | `/api/v1/auth/logout` | Bearer | Thu hồi Refresh Token hiện tại | `204`, `400`, `401`, `403` |

**Ví dụ Request Login:**
```json
{
  "userNameOrEmail": "staff@crmmini.local",
  "password": "<your-password>"
}
```

### 2.2 Users & Teams (`/api/v1/users`, `/api/v1/teams`)
| Method | Route | Auth / Policy | Mô tả | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/users/me` | Authenticated | Lấy hồ sơ người dùng hiện tại | `200`, `401` |
| `PUT` | `/api/v1/users/me` | Authenticated | Cập nhật `fullName`, `phone` cá nhân | `200`, `400`, `401` |
| `PUT` | `/api/v1/users/me/password` | Authenticated | Đổi mật khẩu và thu hồi các refresh token cũ | `204`, `400`, `401`, `422` |
| `GET` | `/api/v1/users` | `ManagerOrAdmin` | Danh sách user (filter: `search`, `role`, `teamId`, `isActive`, `isLocked`, `sortBy`, `sortDir`, `page`, `pageSize`) | `200`, `401`, `403` |
| `GET` | `/api/v1/users/{id}` | `ManagerOrAdmin` | Chi tiết 1 user trong phạm vi quyền | `200`, `403`, `404` |
| `POST` | `/api/v1/users` | `AdminOnly` | Tạo user mới kèm roles và team | `201`, `400`, `403`, `404`, `409` |
| `PUT` | `/api/v1/users/{id}` | `AdminOnly` | Cập nhật user, team, roles, khóa/mở khóa (`isLocked`, `isActive`) | `200`, `400`, `403`, `404`, `422` |
| `GET` | `/api/v1/teams` | `ManagerOrAdmin` | Danh sách nhóm | `200`, `403` |
| `POST` | `/api/v1/teams` | `AdminOnly` | Tạo nhóm mới | `201`, `400`, `403`, `409` |
| `PUT` | `/api/v1/teams/{id}` | `AdminOnly` | Cập nhật thông tin nhóm | `200`, `400`, `403`, `404` |

### 2.3 Companies (`/api/v1/companies`)
| Method | Route | Policy | Query / Body | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/companies` | `StaffOrAbove` | Query: `search`, `industry`, `ownerId`, `sortBy`, `sortDir`, `page`, `pageSize` | `200`, `401` |
| `GET` | `/api/v1/companies/{id}` | `StaffOrAbove` | Chi tiết doanh nghiệp kèm `customerCount` | `200`, `403`, `404` |
| `POST` | `/api/v1/companies` | `StaffOrAbove` | Tạo doanh nghiệp; kiểm tra trùng `taxCode` | `201`, `400`, `403`, `409` |
| `PUT` | `/api/v1/companies/{id}` | `StaffOrAbove` | Cập nhật doanh nghiệp | `200`, `400`, `403`, `404`, `409` |
| `DELETE` | `/api/v1/companies/{id}` | `ManagerOrAdmin` | Soft delete (chặn nếu còn Customer liên kết) | `204`, `403`, `404`, `422` |

### 2.4 Customers (`/api/v1/customers`)
| Method | Route | Policy | Query / Body | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/customers` | `StaffOrAbove` | Query: `search`, `status`, `companyId`, `ownerId`, `teamId`, `sortBy`, `sortDir`, `page`, `pageSize` | `200`, `401` |
| `GET` | `/api/v1/customers/{id}` | `StaffOrAbove` | Chi tiết khách hàng | `200`, `403`, `404` |
| `GET` | `/api/v1/customers/{id}/history` | `StaffOrAbove` | Chi tiết khách hàng + Opportunities + Activities + AuditLogs | `200`, `403`, `404` |
| `POST` | `/api/v1/customers` | `StaffOrAbove` | Tạo khách hàng; kiểm tra trùng `email`/`phone` | `201`, `400`, `403`, `409` |
| `PUT` | `/api/v1/customers/{id}` | `StaffOrAbove` | Cập nhật khách hàng | `200`, `400`, `403`, `404`, `409` |
| `DELETE` | `/api/v1/customers/{id}` | `ManagerOrAdmin` | Soft delete khách hàng | `204`, `403`, `404` |

### 2.5 Leads (`/api/v1/leads`)
| Method | Route | Policy | Query / Body | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/leads` | `StaffOrAbove` | Query: `search`, `status`, `source`, `ownerId`, `teamId`, `sortBy`, `sortDir`, `page`, `pageSize` | `200`, `401` |
| `GET` | `/api/v1/leads/{id}` | `StaffOrAbove` | Chi tiết Lead | `200`, `403`, `404` |
| `POST` | `/api/v1/leads` | `StaffOrAbove` | Tạo Lead (`Status = New`); kiểm tra trùng Lead đang xử lý | `201`, `400`, `403`, `409` |
| `PUT` | `/api/v1/leads/{id}` | `StaffOrAbove` | Cập nhật Lead & chuyển trạng thái theo state machine | `200`, `400`, `403`, `404`, `409`, `422` |
| `POST` | `/api/v1/leads/{id}/assign` | `ManagerOrAdmin` | Body: `{ "ownerId": "..." }` | `200`, `403`, `404`, `422` |
| `POST` | `/api/v1/leads/{id}/convert` | `StaffOrAbove` | Chuyển đổi Lead (`Qualified`) -> Customer (+ Company/Opportunity) | `200`, `400`, `403`, `404`, `409`, `422` |
| `DELETE` | `/api/v1/leads/{id}` | `ManagerOrAdmin` | Soft delete Lead | `204`, `403`, `404` |

### 2.6 Opportunities (`/api/v1/opportunities`)
| Method | Route | Policy | Query / Body | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/opportunities` | `StaffOrAbove` | Query: `search`, `stage`, `customerId`, `ownerId`, `teamId`, `expectedCloseFrom`, `expectedCloseTo`, `sortBy`, `sortDir`, `page`, `pageSize` | `200`, `401` |
| `GET` | `/api/v1/opportunities/{id}` | `StaffOrAbove` | Chi tiết Opportunity | `200`, `403`, `404` |
| `GET` | `/api/v1/opportunities/{id}/history` | `StaffOrAbove` | Chi tiết Opportunity + Activities + AuditLogs | `200`, `403`, `404` |
| `POST` | `/api/v1/opportunities` | `StaffOrAbove` | Tạo cơ hội kinh doanh mới | `201`, `400`, `403`, `404`, `422` |
| `PUT` | `/api/v1/opportunities/{id}` | `StaffOrAbove` | Cập nhật cơ hội (chỉ khi chưa đóng) | `200`, `400`, `403`, `404`, `422` |
| `POST` | `/api/v1/opportunities/{id}/stage` | `StaffOrAbove` | Đổi giai đoạn pipeline (`ClosedLost` bắt buộc `lossReason`; reopen chỉ `Manager`/`Admin`) | `200`, `400`, `403`, `404`, `422` |
| `POST` | `/api/v1/opportunities/{id}/assign` | `ManagerOrAdmin` | Phân công Opportunity cho user khác | `200`, `403`, `404`, `422` |
| `DELETE` | `/api/v1/opportunities/{id}` | `ManagerOrAdmin` | Soft delete Opportunity | `204`, `403`, `404` |

### 2.7 Activities & Dashboard (`/api/v1/activities`, `/api/v1/dashboard`)
| Method | Route | Policy | Query / Body | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/activities` | `StaffOrAbove` | Query: `search`, `type`, `status`, `ownerId`, `customerId`, `leadId`, `opportunityId`, `fromDate`, `toDate`, `overdueOnly`, `sortBy`, `sortDir`, `page`, `pageSize` | `200`, `401` |
| `GET` | `/api/v1/activities/{id}` | `StaffOrAbove` | Chi tiết Activity | `200`, `403`, `404` |
| `POST` | `/api/v1/activities` | `StaffOrAbove` | Tạo Activity (bắt buộc gắn ít nhất 1 target; `scheduledAt` chuẩn UTC/ISO-8601) | `201`, `400`, `403`, `404` |
| `PUT` | `/api/v1/activities/{id}` | `StaffOrAbove` | Cập nhật trạng thái/nội dung Activity | `200`, `400`, `403`, `404` |
| `DELETE` | `/api/v1/activities/{id}` | `StaffOrAbove` | Soft delete (`Staff` chỉ xóa Activity của mình) | `204`, `403`, `404` |
| `GET` | `/api/v1/dashboard/summary` | `StaffOrAbove` | Query: `fromDate`, `toDate`, `timeZoneOffsetMinutes` | `200`, `401` |
| `GET` | `/api/v1/dashboard/pipeline` | `StaffOrAbove` | Thống kê pipeline theo Stage & Owner | `200`, `401` |
| `GET` | `/api/v1/dashboard/activities` | `StaffOrAbove` | Thống kê Activity quá hạn, hôm nay, sắp tới & theo loại | `200`, `401` |

---

## 3. Ví dụ Lỗi Chuẩn Problem Details

**400 Validation Error:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Dữ liệu đầu vào không hợp lệ.",
  "instance": "/api/v1/opportunities/00000000-0000-0000-0000-000000000001/stage",
  "errors": {
    "LossReason": [
      "Bắt buộc nhập lý do thua (LossReason) khi chuyển sang ClosedLost."
    ]
  },
  "errorCode": "validation_error",
  "traceId": "0HN7..."
}
```

**403 Forbidden:**
```json
{
  "title": "Forbidden",
  "status": 403,
  "detail": "Bạn không có quyền truy cập dữ liệu ngoài phạm vi phụ trách.",
  "instance": "/api/v1/customers/...",
  "errorCode": "forbidden",
  "traceId": "0HN7..."
}
```
