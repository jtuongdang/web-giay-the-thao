# BẢN ĐẶC TẢ KIẾN TRÚC HỆ THỐNG THƯƠNG MẠI ĐIỆN TỬ BÁN GIÀY
## (Modular Monolith + Decoupled REST API Architecture Specification)

---

### 1. TỔNG QUAN & MỤC TIÊU DỰ ÁN
Hệ thống **Quản Lý Bán Giày & Vận Hành Kinh Doanh Thương Mại** được thiết kế để đưa vào vận hành thực tế cho cửa hàng bán lẻ giày thể thao, quản trị khép kín từ khâu **Nhập hàng & Công nợ NCC -> Quản lý Kho theo Size/Màu -> Bán lẻ Storefront -> Đổi trả/Hoàn tiền -> Quản lý Dòng tiền (Thu/Chi/Lãi gộp)**.

Hệ thống tuân thủ 3 nguyên tắc kiến trúc thực dụng:
1. **Modular Monolith trên ASP.NET Core 10 (`QuanLyBanGiay.csproj`) + RESTful API tách lớp**:
   - Vận hành gọn nhẹ trên 1 Web App duy nhất (`QuanLyBanGiay`) để dễ triển khai và bảo trì cho shop, nhưng phân tách rạch ròi theo module (`Models/Entities`, `Data`, `Services`, `Controllers/Api`, `Views/Home`, `Views/Cart`, `Views/Admin`).
   - Toàn bộ nghiệp vụ cốt lõi được phục vụ qua **RESTful JSON API (`/api/v1/*`)**, sẵn sàng tách thành Frontend SPA/Mobile độc lập về sau.
2. **Bảo mật & Phân quyền chuẩn thương mại (Auth & RBAC)**:
   - Xác thực bằng **JWT Bearer** (cho API) và/hoặc Cookie Auth (cho Admin Portal), phân tách vai trò `Admin`, `Staff`, `Customer`.
   - Toàn bộ API quản trị (`/api/v1/admin/*`) và API mô phỏng thanh toán (`/api/v1/payments/mock-simulate`) bắt buộc xác thực quyền `Admin`/`Staff`.
3. **Tối ưu vận hành thực tế (Lean Operations)**:
   - **Bỏ hoàn toàn tính toán thuế (VAT = 0%)**: `TaxRate = 0`, `TaxAmount = 0`. `FinalTotal = Subtotal - DiscountAmount + ShippingFee`.
   - **Mô phỏng thanh toán (Mock Payment Engine)**: Tự chủ quy trình thanh toán giả lập (COD, Chuyển khoản VietQR mô phỏng, Thẻ mô phỏng) không phụ thuộc sandbox ngân hàng.

---

### 2. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ (HIGH-LEVEL ARCHITECTURE)

```mermaid
graph TD
    subgraph ClientLayer ["1. TẦNG GIAO DIỆN (STOREFRONT & ADMIN PORTAL)"]
        Storefront["🌐 Web Khách hàng (Storefront)<br/>- Tra cứu, Lọc giày theo Hãng/Size/Màu/Giá<br/>- Kiểm tra tồn kho khả dụng (Stock - Reserved)<br/>- Giỏ hàng, Áp Voucher & Đặt hàng"]
        AdminPortal["💼 Web Quản trị (Admin Backoffice)<br/>- Nhập hàng & Trả nợ Nhà cung cấp<br/>- Xử lý đơn, Đổi size / Hoàn tiền (RMA)<br/>- Quản lý Kho & Báo cáo Dòng tiền / Lãi lỗ"]
    end

    subgraph SecurityLayer ["2. GATEWAY, BẢO MẬT & VALIDATION"]
        AuthRBAC["Auth & RBAC Middleware<br/>(JWT Bearer / Cookie + Role: Admin, Staff, Customer)"]
        RestAPI["RESTful API Controllers (/api/v1/*)<br/>(Auth, Products, Vouchers, Orders, Admin/*)"]
    end

    subgraph BusinessServices ["3. TẦNG NGHIỆP VỤ (APPLICATION SERVICES)"]
        CatalogService["Catalog & Variant Service"]
        OrderService["Order & Stock Reservation Engine<br/>(State Machine + Concurrency Control)"]
        RmaService["Return & Exchange (RMA) Service"]
        PaymentMock["Mock Payment Provider<br/>(COD / QR Simulator / Card Mock)"]
        InventoryService["Inventory & Stock Ledger Engine"]
        CashFlowService["Cash Flow, Debt & Profit Engine<br/>(Thu/Chi/Công nợ NCC/COGS/Gross Profit)"]
    end

    subgraph DataLayer ["4. TẦNG DỮ LIỆU (SQL SERVER 2022)"]
        EFCore["Entity Framework Core 10<br/>(AppDbContext + ACID Transactions + RowVersion)"]
        Database[("SQL Server 2022<br/>(Docker: musing_volhard / QuanLyBanGiayDb)")]
    end

    Storefront -->|HTTP/JSON| AuthRBAC
    AdminPortal -->|HTTP/JSON + JWT/Cookie| AuthRBAC
    AuthRBAC --> RestAPI
    RestAPI --> CatalogService
    RestAPI --> OrderService
    RestAPI --> RmaService
    RestAPI --> InventoryService
    RestAPI --> CashFlowService
    OrderService --> PaymentMock
    OrderService --> InventoryService
    OrderService --> CashFlowService
    RmaService --> InventoryService
    RmaService --> CashFlowService
    InventoryService --> CashFlowService

    CatalogService --> EFCore
    OrderService --> EFCore
    RmaService --> EFCore
    InventoryService --> EFCore
    CashFlowService --> EFCore
    EFCore --> Database
```

---

### 3. CẤU TRÚC THƯ MỤC CHUẨN TRONG PROJECT (`QuanLyBanGiay`)

```text
QuanLyBanGiay/
├── Data/
│   ├── AppDbContext.cs          # EF Core DbContext, Fluent API, Constraints, Indexes
│   ├── DbInitializer.cs         # Seed dữ liệu mẫu (Admin user, Shoes, Variants, Suppliers, Vouchers)
│   └── Migrations/              # EF Core SQL Server Migrations
├── Models/
│   ├── Entities/                # Database Entities (User, Product, ProductVariant, Voucher, Order, ...)
│   ├── DTOs/                    # Request/Response Contracts & ApiResponse<T>
│   └── ViewModels/              # MVC ViewModels (Storefront & Admin)
├── Services/
│   ├── Auth/                    # Đăng nhập, tạo JWT, băm mật khẩu (PasswordHasher)
│   ├── Catalog/                 # Lọc sản phẩm, kiểm tra tồn khả dụng
│   ├── Inventory/               # Nhập kho, xuất kho, giữ chỗ, sổ cái InventoryTransactions
│   ├── Orders/                  # Đặt hàng, State Machine trạng thái đơn, Concurrency
│   ├── Returns/                 # Nghiệp vụ Đổi size & Hoàn tiền (RMA)
│   ├── Finance/                 # Sổ quỹ CashFlowLedger, Công nợ NCC, Báo cáo COGS & Lãi gộp
│   └── Payment/                 # Mock Payment Engine (COD, QR, Card)
├── Controllers/
│   ├── Api/                     # RESTful API Controllers (/api/v1/*)
│   ├── HomeController.cs        # Storefront Pages
│   ├── CartController.cs        # Cart & Checkout Pages
│   └── AdminController.cs       # Admin Backoffice Pages
├── Views/
│   ├── Home/
│   ├── Cart/
│   └── Admin/                   # Giao diện Quản trị Kho, Nhập hàng, Đơn hàng, Tài chính
└── wwwroot/                     # Assets, CSS, JS (Storefront & Admin Charts)
```

---

### 4. CƠ CHẾ THANH TOÁN MÔ PHỎNG & MIỄN THUẾ
1. **Miễn thuế (`VAT = 0%`)**:
   - Mọi đơn hàng áp dụng `TaxRate = 0`, `TaxAmount = 0`.
   - `TotalAmount = Subtotal - DiscountAmount + ShippingFee`.
2. **Mô phỏng thanh toán (`Mock Payment Engine`)**:
   - **COD**: Tạo đơn với `PaymentStatus = Pending`. Khi đơn chuyển sang `Completed` (đã giao & thu tiền), hệ thống tự động chuyển `PaymentStatus = Paid` và ghi phiếu thu `RECEIPT` vào `CashFlowLedger`.
   - **QR Code Chuyển khoản (`QR_TRANSFER`)**: Sinh thông tin QR kèm mã đơn `DH-YYYYMMDD-XXX` và số tiền. Endpoint xác nhận thanh toán `POST /api/v1/payments/mock-simulate` chỉ cho phép role `Admin`/`Staff` (hoặc môi trường `Development`) kích hoạt để tránh gian lận đơn hàng.
   - **Thẻ giả lập (`MOCK_CARD`)**: Hỗ trợ kiểm thử luồng thanh toán thành công/thất bại tức thì trong môi trường phát triển.
