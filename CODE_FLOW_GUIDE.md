# CulinaryProject – Code Flow Guide (Chi tiết)

## 1) Tổng quan kiến trúc

Monorepo hiện có các phần chính:

- `CulinaryBackend/CulinaryBackend`: ASP.NET Core Web API (MongoDB).
- `CulinaryAdmin/CulinaryAdmin`: Blazor Admin/Owner portal (MudBlazor).
- `CulinaryApp/CulinaryApp`: MAUI mobile app (client tiêu thụ API backend).

Luồng tổng quát:

1. Người dùng đăng nhập từ Admin UI.
2. Admin/Owner thao tác POI/User qua API backend.
3. Backend ghi dữ liệu MongoDB và gửi email notification theo event.
4. Mobile app lấy danh sách POI qua API, và track lượt truy cập POI.

---

## 2) Backend startup và DI

File chính: `CulinaryBackend/CulinaryBackend/Program.cs`

### Khởi tạo chính

- Cấu hình CORS mở (`AllowAnyOrigin/Method/Header`).
- Khởi tạo `MongoClient` từ connection string.
- Đăng ký singleton `IMongoDatabase` (`CulinaryDB`).
- Đăng ký services:
  - `PoiService`
  - `EmailService`
  - `PoiVisitService`
- Map controllers + Swagger.

Ý nghĩa:

- Tất cả controller/service dùng chung 1 `IMongoDatabase` đã đăng ký DI.
- Tránh việc mỗi service tự dựng client mới sai config.

---

## 3) Luồng Authentication và User

### 3.1 Đăng ký/đăng nhập

Controller: `CulinaryBackend/CulinaryBackend/Controllers/AuthController.cs`

- `POST /api/Auth/register`
  - Hash password với BCrypt.
  - Lưu `UserModel`.
- `POST /api/Auth/login`
  - Kiểm tra username/password.
  - Chặn user bị khóa (`IsActive = false`).

Model liên quan:

- `CulinaryBackend/CulinaryBackend/Models/UserModel.cs`
  - `Id, Username, PasswordHash, Role, OwnerId, FullName, IsActive, CreatedAt`.

### 3.2 Quản lý user (admin)

Controller: `CulinaryBackend/CulinaryBackend/Controllers/UserController.cs`

- `GET /api/User`: lấy danh sách user.
- `PUT /api/User/{id}`: cập nhật role/fullName/ownerId/isActive.
- `PATCH /api/User/{id}/toggle-active`: khóa/mở khóa nhanh.
- `DELETE /api/User/{id}`: xóa user.

Email user-event đã gắn:

- Khi đổi trạng thái khóa/mở khóa (PUT hoặc PATCH toggle-active) sẽ gửi mail.
- Khi DELETE user sẽ gửi mail.

UI trang quản lý user:

- `CulinaryAdmin/CulinaryAdmin/Components/Pages/UserManager.razor`
- Nút xóa hiện gọi `DELETE /api/User/{id}` trực tiếp.

---

## 4) Luồng POI (Create/Read/Update/Delete/Approve/Reject/Restore)

Controller chính: `CulinaryBackend/CulinaryBackend/Controllers/PoiController.cs`

### 4.1 Đọc danh sách POI

- `GET /api/Poi?lang=vi&includeDeleted=false`
  - Mặc định **ẩn POI đã xóa** (`status = deleted`).
  - Nếu `includeDeleted=true` thì trả cả POI deleted.
- `GET /api/Poi/nearby?...&includeDeleted=false`
  - Tương tự: mặc định ẩn deleted.

Hydration/fallback:

- Hàm `HydrateAndFallback(poi, lang)` fallback ngôn ngữ: `lang -> en -> vi`.
- Nếu thiếu title thì trả chuỗi rỗng (đã bỏ placeholder “Đang cập nhật...”).

### 4.2 Tạo POI

- `POST /api/Poi`
- Rule:
  - Bắt buộc có `OwnerId` (admin không được tạo POI trực tiếp).
  - Tự set `Status = pending`.

Email:

- Gửi mail event tạo POI.
- Gửi mail cho owner (nếu username là email).

### 4.3 Cập nhật POI

- `PUT /api/Poi/{id}`
- Update dữ liệu phẳng từ Admin vào `Localizations["vi"]`, ảnh, location.

Email:

- Gửi mail event cập nhật + mail cho owner.

### 4.4 Xóa/khôi phục/duyệt/từ chối

- `DELETE /api/Poi/{id}`: không hard delete; set `status = deleted`.
- `PATCH /api/Poi/{id}/restore`: set `status = pending`.
- `PATCH /api/Poi/{id}/approve`: set `status = approved`.
- `PATCH /api/Poi/{id}/reject`: set `status = rejected`.

Email:

- Delete: gửi event mail + mail owner.
- Restore: gửi event mail + mail owner.
- Approve/Reject: gửi event mail + mail owner (approval/rejection template).

---

## 5) Luồng POI Visit / thống kê quan tâm

### 5.1 Track visit

Controller: `CulinaryBackend/CulinaryBackend/Controllers/PoiVisitController.cs`

- `POST /api/PoiVisit/{poiId}`: ghi 1 record visit.
- Lưu vào collection `PoiVisits`.

Service: `CulinaryBackend/CulinaryBackend/Services/PoiVisitService.cs`

- `TrackVisitAsync`: insert `PoiVisit`.
- `GetVisitStatsAsync`: aggregate group theo `poiId`, sort giảm dần.
- Đã fix cast an toàn `_id` khi Mongo trả `ObjectId` hoặc `string`.

### 5.2 Lấy thống kê

- `GET /api/PoiVisit/stats`.
- Admin dashboard và owner dashboard consume endpoint này.

---

## 6) Admin UI flow

### 6.1 Dashboard

File: `CulinaryAdmin/CulinaryAdmin/Components/Pages/AdminDashboard.razor`

Hiển thị:

- KPI cards (POI, admin, owner, account lock).
- Phân bổ danh mục quán.
- Danh sách “Khác” (liệt kê title POI vào category khác).
- Top quán được quan tâm (visit stats).

### 6.2 POI Manager

File: `CulinaryAdmin/CulinaryAdmin/Components/Pages/PoiManager.razor`

Rule hiện tại:

- Admin không có thao tác tạo/sửa trực tiếp trên UI (đã bỏ luồng edit).
- Có thể xem chi tiết, mở map, xóa, restore.
- Load danh sách dùng `includeDeleted=true` để thấy cả item deleted và restore lại.

Wording:

- Trạng thái deleted hiển thị “Đã xóa” (không dùng “mềm”).

---

## 7) Owner UI flow

### 7.1 My POIs

File: `CulinaryAdmin/CulinaryAdmin/Components/Pages/MyPois.razor`

Luồng:

1. Lấy user hiện tại từ auth state.
2. Gọi `api/User` tìm `OwnerId` theo username.
3. Gọi `api/Poi?lang=vi&includeDeleted=true`.
4. Filter theo `p.OwnerId == currentOwnerId`.

Owner có thể:

- Tạo POI mới (sẽ vào `pending`).
- Sửa POI của mình.
- Xóa/restore POI của mình.
- Mở popup chi tiết (đồng thời track visit).

### 7.2 Owner Dashboard

File: `CulinaryAdmin/CulinaryAdmin/Components/Pages/OwnerDashboard.razor`

- Tính KPI theo POI owner.
- Lấy visit stats và lọc theo POI owner.
- Dùng endpoint mặc định (`includeDeleted=false`) nên POI deleted không hiện trên dashboard.

---

## 8) Email service flow

Service: `CulinaryBackend/CulinaryBackend/Services/EmailService.cs`

Các method chính:

- `SendApprovalEmailAsync(ownerEmail, poiTitle, isApproved)`
- `SendPoiEventEmailAsync(poiTitle, actionName, status)`
- `SendUserAccountEventEmailAsync(userEmail, username, action)`
- `SendCustomEmailAsync(toEmail, subject, body)`

SMTP config keys:

- `Email:SmtpHost`
- `Email:SmtpPort`
- `Email:FromEmail`
- `Email:Password`
- `Email:NotificationEmail` (optional)

Lưu ý:

- Nếu username user không phải email hợp lệ, các mail owner/user sẽ bị skip.

---

## 9) Quy ước trạng thái POI

- `pending`: chờ duyệt.
- `approved`: đã duyệt.
- `rejected`: từ chối.
- `deleted`: đã xóa (ẩn mặc định khỏi endpoint list/nearby).

---

## 10) Checklist test nhanh sau deploy

1. Backend chạy, test:
   - `GET /api/Poi?lang=vi`
   - `GET /api/Poi?lang=vi&includeDeleted=true`
   - `GET /api/PoiVisit/stats`
   - `GET /api/User`
2. Owner:
   - Tạo POI -> status pending.
   - Xóa POI -> biến mất ở dashboard/public list.
   - Vào MyPois (includeDeleted=true) -> thấy trạng thái đã xóa, restore được.
3. Admin:
   - POI manager thấy cả deleted và restore được.
   - Không có hành động tạo/sửa POI trong manager.
4. Email:
   - Approve/Reject POI nhận mail owner.
   - Delete/Restore POI nhận mail owner.
   - Lock/Unlock/Delete user nhận mail.

---

## 11) Gợi ý cải tiến tiếp theo

- Thêm `isDeleted` boolean + `deletedAt` để audit rõ hơn.
- Thêm trang "Trash" riêng thay vì includeDeleted trong page chính.
- Chuẩn hóa username/email tách riêng trường `Email` trong `UserModel`.
- Thêm test integration cho các endpoint status chuyển trạng thái.
