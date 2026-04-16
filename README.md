# PRODUCT REQUIREMENTS DOCUMENT: FOOD MAP

## 1. TỔNG QUAN
Dự án **FOOD MAP** là ứng dụng bản đồ du lịch thông minh, giúp du khách dễ dàng tìm kiếm, dẫn đường và tương tác ảo bằng thuyết minh giọng nói (TTS) tại các địa điểm tham quan cũng như khám phá thực đơn các cơ sở ẩm thực địa phương.

**Mục tiêu:**
- Số hóa các trải nghiệm tại điểm đến (Thuyết minh tự động, quét QR, chỉ đường, xem thực đơn).
- Hoạt động ổn định ngay cả trong điều kiện mạng chập chờn (Offline Queue Sync).
- Đa ngôn ngữ và Bản quyền dịch thông qua cơ chế cộng tác viên.

## 2. KIẾN TRÚC HỆ THỐNG
Dự án xây dựng trên nền tảng .NET MAUI và mô hình CSDL lõi PostgreSQL với thiết kế Clean Architecture (Repository Pattern), hỗ trợ cơ chế đồng bộ ngầm khi mất mạng.

![Architecture Diagram](FOOD_MAP/Resources/Images/architecture_diagram.png)

| Lớp | Thành phần | Trách Nhiệm |
| --- | --- | --- |
| **Presentation** | MainPage, LoginPage, RegisterPage, SettingsPage | Giao diện hiển thị, tương tác người dùng |
| **ViewModel** | MainPageViewModel, SettingsViewModel, ... | Business logic giao diện gốc, Data binding |
| **Service** | AuthService, NarrationService, DataService | Application logic, tính năng cốt lõi đa tầng |
| **Repository** | PoiRepository, UserActivityRepository, SyncQueue | Truy cập dữ liệu, quản lý Offline Queue |
| **Data** | AppDbContext (EF Core + Npgsql), PostgreSQL | Persistent Layer cục bộ/đám mây |

## 3. TÁC NHÂN HỆ THỐNG
Hệ thống được thiết kế để phục vụ tệp khách hàng đa dạng hóa và cộng tác viên nội dung thông qua 4 quyền (Actor) chính:

| Actor | Mô Tả | Quyền Hạn |
| --- | --- | --- |
| **Guest** | Khách trải nghiệm vô danh | Xem bản đồ POI, quét QR điểm tham quan, nghe thuyết minh TTS, xem menu món ăn. Kế thừa giới hạn không lưu vết. |
| **User** | Đã đăng ký & đăng nhập | Tất cả quyền của Guest + Lưu địa điểm Yêu thích (Favorite), Ghi nhận Lịch sử di chuyển (Tour), Cập nhật Hồ sơ cá nhân, Nâng cấp Subscription. |
| **Owner** | Đã được Admin phê duyệt | Tất cả quyền của User + Tạo/Chỉnh sửa địa điểm (POI), Quản lý Thực đơn nhà hàng, Xin cấp phép ngôn ngữ dịch thuật, Mua gói VIP cho doanh nghiệp. |
| **Admin** | Quản trị viên hệ thống | Phê duyệt/Từ chối duyệt địa điểm POI, Duyệt Owner, Duyệt quyền sở hữu bản dịch Ngôn ngữ, Quản trị hệ thống dòng tiền và gói Subscription. |

## 4. CẤU TRÚC CHỨC NĂNG VÀ VÒNG ĐỜI
Sơ đồ hệ thống được rẽ nhánh và xây dựng dựa trên cốt lõi của 3 loại dữ liệu chính là **Định danh User, Dữ liệu Địa điểm, và Thực đơn món ăn**.

### 4.1 Sơ Đồ Cây Phụ Thuộc (Dependency Tree)
Đây là sơ đồ nhánh cây (WBS) minh chứng cho thực trạng: Tại sao thiếu vắng 3 Root chính thì tất cả các chức năng cộng sinh khác (Gia hạn gói dịch vụ, QR Code, Đồng bộ offline...) sẽ không thể cất cánh.

![Dependency Tree Model](FOOD_MAP/Resources/Images/dependency_tree.png)

### 4.2 Vòng Đời Trải Nghiệm (Activity Lifecycle)
Sơ đồ luồng luân chuyển dữ liệu theo thời gian thực đi qua trọn vẹn 17 quy trình chuẩn (Use Case). Trải dài từ quá trình chuẩn bị dữ liệu kỹ càng của đội ngũ quản trị, cho đến lúc bước chân lên đường của Khách du lịch.

![Product Lifecycle Activity](FOOD_MAP/Resources/Images/lifecycle_activity.png)

## 5. MÔ HÌNH DỮ LIỆU (ERD)
Bức tranh sơ đồ quan hệ Entity của dự án với PostgreSQL.

![Entity Relationship Diagram](FOOD_MAP/Resources/Images/data_model_diagram.png)

| Bảng | Mô Tả | Tác Dụng |
| --- | --- | --- |
| **Users** | Tài khoản người dùng | Xác thực thông tin cá nhân. |
| **POIs** | Điểm tham quan/quán ăn | Lõi thông tin định vị và thông tin chung yếu. |
| **POITranslations** | Ngôn ngữ dịch | Lưu trữ thông tin nội dung đã được dịch thuật. |
| **FoodItems** | Từng món ăn nhỏ | Liên kết theo mỗi POI chuẩn nhà hàng. |
| **UserTours/Favorites** | Nhật ký | Truy vết và kết xuất lộ trình tham quan cho khách. |
| **SyncHistories** | Log | Lịch sử đồng bộ hệ thống. |
| **Subscriptions** | Gói VIP | Quản lý kỳ hạn gói thanh toán (Tier). |
| **... Requests** | Duyệt Yêu Cầu | Quản trị các lá đơn từ Owner/User lên Admin. |

---

## 6. ACTIVITY DIAGRAMS — SƠ ĐỒ HOẠT ĐỘNG (17 Chức Năng)

<details>
<summary><b>Nhấn vào đây để xem toàn bộ 17 Sơ Đồ Hoạt Động</b></summary>

### 6.1 Đăng Nhập / Đăng Ký / Tiếp Tục Khách
![act_01_login](FOOD_MAP/Resources/Images/act_01_login.png)

### 6.2 Xem Bản Đồ POI
![act_02_viewmap](FOOD_MAP/Resources/Images/act_02_viewmap.png)

### 6.3 Tìm Kiếm Địa Điểm
![act_03_search](FOOD_MAP/Resources/Images/act_03_search.png)

### 6.4 Phát Thuyết Minh TTS
![act_04_tts](FOOD_MAP/Resources/Images/act_04_tts.png)

### 6.5 Quét Mã QR Code
![act_05_qr](FOOD_MAP/Resources/Images/act_05_qr.png)

### 6.6 Chỉ Đường GPS & Ghi Nhận Tour
![act_06_gps](FOOD_MAP/Resources/Images/act_06_gps.png)

### 6.7 Đánh Dấu Yêu Thích 
![act_07_favorite](FOOD_MAP/Resources/Images/act_07_favorite.png)

### 6.8 Thay Đổi Ngôn Ngữ
![act_08_language](FOOD_MAP/Resources/Images/act_08_language.png)

### 6.9 Xem Thực Đơn Ẩm Thực
![act_09_menu](FOOD_MAP/Resources/Images/act_09_menu.png)

### 6.10 Quản Lý Hồ Sơ
![act_10_profile](FOOD_MAP/Resources/Images/act_10_profile.png)

### 6.11 Đăng Ký Làm Chủ Cơ Sở
![act_11_ownerrequest](FOOD_MAP/Resources/Images/act_11_ownerrequest.png)

### 6.12 Quản Lý Địa Điểm POI (Owner)
![act_12_managepoi](FOOD_MAP/Resources/Images/act_12_managepoi.png)

### 6.13 Quản Lý Thực Đơn (Food Manager)
![act_13_foodmanager](FOOD_MAP/Resources/Images/act_13_foodmanager.png)

### 6.14 Yêu Cầu Quyền Ngôn Ngữ Dịch
![act_14_langownership](FOOD_MAP/Resources/Images/act_14_langownership.png)

### 6.15 Admin Phê Duyệt Yêu Cầu
![act_15_admin](FOOD_MAP/Resources/Images/act_15_admin.png)

### 6.16 Đồng Bộ Dữ Liệu Offline Tự Động
![act_16_offlinesync](FOOD_MAP/Resources/Images/act_16_offlinesync.png)

### 6.17 Quản Lý Gói Dịch Vụ (Subscription)
![act_17_subscription](FOOD_MAP/Resources/Images/act_17_subscription.png)

</details>

---

## 7. SEQUENCE DIAGRAMS — SƠ ĐỒ TRÌNH TỰ (17 Chức Năng)

<details>
<summary><b>Nhấn vào đây để xem toàn bộ 17 Sơ Đồ Trình Tự</b></summary>

### 7.1 Đăng Nhập / Đăng Ký
![seq_01_login](FOOD_MAP/Resources/Images/seq_01_login.png)

### 7.2 Xem Bản Đồ POI
![seq_02_viewmap](FOOD_MAP/Resources/Images/seq_02_viewmap.png)

### 7.3 Tìm Kiếm Địa Điểm
![seq_03_search](FOOD_MAP/Resources/Images/seq_03_search.png)

### 7.4 Phát Thuyết Minh TTS
![seq_04_tts](FOOD_MAP/Resources/Images/seq_04_tts.png)

### 7.5 Quét Mã QR Code
![seq_05_qr](FOOD_MAP/Resources/Images/seq_05_qr.png)

### 7.6 Chỉ Đường GPS & Ghi Nhận Tour
![seq_06_gps](FOOD_MAP/Resources/Images/seq_06_gps.png)

### 7.7 Đánh Dấu Yêu Thích 
![seq_07_favorite](FOOD_MAP/Resources/Images/seq_07_favorite.png)

### 7.8 Thay Đổi Ngôn Ngữ
![seq_08_language](FOOD_MAP/Resources/Images/seq_08_language.png)

### 7.9 Xem Thực Đơn Ẩm Thực
![seq_09_menu](FOOD_MAP/Resources/Images/seq_09_menu.png)

### 7.10 Quản Lý Hồ Sơ
![seq_10_profile](FOOD_MAP/Resources/Images/seq_10_profile.png)

### 7.11 Đăng Ký Làm Chủ Cơ Sở
![seq_11_ownerrequest](FOOD_MAP/Resources/Images/seq_11_ownerrequest.png)

### 7.12 Quản Lý Địa Điểm POI (Owner)
![seq_12_managepoi](FOOD_MAP/Resources/Images/seq_12_managepoi.png)

### 7.13 Quản Lý Thực Đơn (Food Manager)
![seq_13_foodmanager](FOOD_MAP/Resources/Images/seq_13_foodmanager.png)

### 7.14 Yêu Cầu Quyền Ngôn Ngữ Dịch
![seq_14_langownership](FOOD_MAP/Resources/Images/seq_14_langownership.png)

### 7.15 Admin Phê Duyệt Yêu Cầu
![seq_15_admin](FOOD_MAP/Resources/Images/seq_15_admin.png)

### 7.16 Đồng Bộ Dữ Liệu Offline Tự Động
![seq_16_offlinesync](FOOD_MAP/Resources/Images/seq_16_offlinesync.png)

### 7.17 Quản Lý Gói Dịch Vụ (Subscription)
![seq_17_subscription](FOOD_MAP/Resources/Images/seq_17_subscription.png)

</details>

---

## 8. LUỒNG TRẠNG THÁI (STATE TRANSITIONS)

### 8.1 Trạng Thái Sinh Tử POI
| Từ Trạng Thái | Hành Động | Đến Trạng Thái |
| --- | --- | --- |
| (new) | Owner đệ trình cơ sở vật chất | Pending |
| Pending | Admin rà soát - Khớp chứng từ | Approved |
| Pending | Tố cáo/Kiểm duyệt ảo | Rejected |

### 8.2 Trạng Thái Xác Thực Owner
| Từ Trạng Thái | Hành Động | Đến Trạng Thái |
| --- | --- | --- |
| (new) | User gửi hồ sơ kinh doanh | Pending |
| Pending | Trùng bộ thông tin thuế -> OK | Approved + Role Owner |

### 8.3 Trạng Thái Đồng Bộ Queue (Mất Mạng)
| Từ Trạng Thái | Mô Tả Dữ Liệu Hoán Vị |
| --- | --- |
| Queued | Lưu cứng trong Cấu hình Offline JSON, tự động thức giấc cắn mạng. |
| Processing | Xử lý HTTP RetryPolicy. |
| Flushed | Bàn giao Server, gỡ dấu vết JSON. |
| Retry | Nếu HTTP TimeOut, tự dời vòng đời lên Exponential 2^n. |

### 8.4 Trạng Thái Thanh Toán Gói Dịch Vụ (Subscription Tier)
| Từ Trạng Thái | Hành Động | Đến Trạng Thái |
| --- | --- | --- |
| (new) | User quẹt thẻ thanh toán | Pending |
| Pending | Xác thực tín dụng Bank / Stripe OK | Active (Giao dịch hoàn tất) |
| Active | Check cron job định kỳ bị lố thời gian hiệu lực | Expired |

## 9. YÊU CẦU PHI CHỨC NĂNG
- **Nghệ thuật bảo mật**: Mật khẩu SHA-256 mã hóa đơn chiều, XSS Protection gắt gao cho mọi TextEditor API đầu vào.
- **UI/UX 60fps**: Các mảng giao diện Modal/BottomSheet/Trang chuyển tiếp sử dụng độ nẩy nội suy `CubicInOut` từ 180ms - 260ms.
- **Tiên phong Offline First**: Mọi cú "Chạm Tym" vào Database, thao tác "Lưu Tuyến Dẫn Đường" đều được giả lập đồng thuận (UI Update > Save Cục Bộ) -> Tốc độ phản hồi cực đoan tức thì `~3ms`.

## 10. PHỤ LỤC & TÀI KHOẢN DEMO
Thật tuyệt vời vì app đã trang bị cấu hình sẵn sàng qua biến `.env` cũng như tự Seed data vào DB nếu ứng dụng chưa từng được khởi chạy.
Quý khách có thể truy cập `demo` - pass `123456` với quyền truy hồi User cơ bản.

> Cảm ơn tất cả!
