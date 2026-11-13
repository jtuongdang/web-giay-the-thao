# BIÊN BẢN BÀN GIAO DỰ ÁN VẬN HÀNH THƯƠNG MẠI
## HỆ THỐNG QUẢN LÝ BÁN GIÀY & ĐIỀU HÀNH KINH DOANH (QUANLYBANGIAY)
**Thời gian hoàn thành:** 2026-11-13 11:03:48
**Vai trò nghiệm thu:** Tech Lead & Ban Điều Phối Đa Agent (DevAgent Orchestrator)
**Đối tượng bàn giao:** Chủ đầu tư & Quản trị viên Cửa hàng

---

### 1. TỔNG QUAN KẾT QUẢ TRIỂN KHAI
Hệ thống Thương mại Điện tử & Quản trị Bán giày đã được 3 Nhân viên AI (`Codex CLI`, `GitHub Copilot Executive`, `Google Antigravity CLI`) triển khai và hoàn tất 100% mục tiêu Phase 2 & Phase 3 theo chuẩn đặc tả nghiệp vụ cao nhất:

* **Tình trạng biên dịch (.NET 10 SDK):** ✓ BUILD THÀNH CÔNG (0 LỖI)
* **Hạ tầng Cơ sở Dữ liệu:** Microsoft SQL Server 2022 (Docker container `musing_volhard`), Database `QuanLyBanGiayDb` (15 bảng dữ liệu quan hệ chặt chẽ).
* **Kiến trúc:** Modular Monolith + Decoupled RESTful API (`/api/v1/*`).

---

### 2. BẢNG TIẾN ĐỘ & NGHIỆM THU CHI TIẾT TỪNG TÁC VỤ

| Mã Task | Tên Nghiệp vụ | Nhân viên AI đảm nhiệm | Trạng thái Nghiệm thu |
| :--- | :--- | :--- | :--- |
| **TASK-01** | Xây dựng Auth & RBAC Service (JWT/Cookie & Phân quyền 3 cấp) | `Codex CLI` | **COMPLETED** |
| **TASK-02** | Bộ máy Quản lý Kho, Nhập hàng (PO) & Công nợ Nhà cung cấp | `Codex CLI` | **COMPLETED** |
| **TASK-03** | Bộ máy Đặt hàng, Khóa tồn kho (Concurrency), Đổi trả (RMA) & Mock Payment | `GitHub Copilot Executive` | **COMPLETED** |
| **TASK-04** | Phát triển toàn bộ RESTful API Controllers chuẩn thương mại (/api/v1/*) | `GitHub Copilot Executive` | **COMPLETED** |
| **TASK-05** | Đấu nối Giao diện Khách hàng (Storefront) vào Database thật & API | `Codex CLI` | **COMPLETED** |
| **TASK-06** | Xây dựng Giao diện Quản trị Cửa hàng (Admin Backoffice Portal) | `Google Antigravity CLI` | **COMPLETED** |

---

### 3. CÁC PHÂN HỆ NGHIỆP VỤ THƯƠNG MẠI ĐÃ ĐƯA VÀO VẬN HÀNH

#### A. Phân hệ Xác thực & Phân quyền (Auth & RBAC)
* Cung cấp JWT Bearer Token cho toàn bộ REST API và Cookie Session cho Admin Portal.
* Phân quyền 3 vai trò độc lập:
  * `Customer`: Khách vãng lai và thành viên đăng ký mua sắm, theo dõi lịch sử đơn.
  * `Staff`: Nhân viên bán hàng, tạo đơn đổi trả, duyệt đơn COD.
  * `Admin`: Chủ shop toàn quyền quản lý kho, nhập hàng, quản lý công nợ và xem sổ quỹ.

#### B. Phân hệ Quản trị Kho 2 Pha & Khóa Concurrency (Stock Reservation)
* **Chống bán âm kho tuyệt đối:** Áp dụng khóa lạc quan `RowVersion` trên từng biến thể Size/Màu (`ProductVariants`).
* **Two-Phase Reservation:** Khi khách tạo đơn, số lượng đưa vào `reserved_quantity`; khi thanh toán hoàn tất mới trừ vào `stock_quantity` thật và ghi log sổ cái kho `InventoryTransactions`.

#### C. Phân hệ Nhập hàng (PO) & Công nợ Nhà Cung Cấp
* Lập đơn đặt hàng nhà cung cấp (`PurchaseOrders`).
* Tự động tính **Giá vốn bình quân gia quyền (Weighted Average Cost)** khi nhập kho theo từng size.
* Ghi nhận công nợ NCC (`current_debt`), hỗ trợ tạo phiếu chi thanh toán nợ và tự động khấu trừ vào sổ quỹ `CashFlowLedger`.

#### D. Phân hệ Đổi Size & Hoàn Tiền (RMA Lifecycle)
* Quy trình đổi size thông minh: Kiểm tra tồn kho khả dụng của size mới, hoàn size cũ về kho, cấn trừ chênh lệch giá.
* Quy trình hoàn hàng & hoàn tiền: Nhập hàng trả lại về kho, sinh phiếu chi hoàn tiền `REFUND` và điều chỉnh giảm doanh thu thuần.

#### E. Cơ chế Miễn thuế & Thanh toán Giả lập (Lean Retail)
* **Bỏ thuế hoàn toàn (`VAT = 0%`)**: Đúng với mô hình bán lẻ thực tế. `Tổng thanh toán = Tạm tính - Voucher + Phí ship`.
* **Thanh toán COD & QR VietQR mô phỏng**: Sinh mã QR chuẩn ngân hàng kèm mã đơn `DH-YYYYMMDD-XXX` tự động ghi nhận phiếu thu.

#### F. Bộ máy Báo cáo Sổ Quỹ & Lãi Gộp (Financial Ledger)
* Tự động hạch toán doanh thu và giá vốn COGS tại thời điểm hoàn tất đơn hàng.
* Công thức tính lãi chuẩn kế toán: $\text{Gross Profit} = \text{Net Revenue} - \text{Total COGS}$.

---

### 4. HƯỚNG DẪN KHỞI CHẠY HỆ THỐNG TRÊN MÁY CHỦ
1. **Đảm bảo SQL Server đang chạy:**
   ```bash
   docker start musing_volhard
   ```
2. **Khởi chạy Web App ASP.NET Core:**
   ```bash
   cd /Users/jtuong/Documents/quanlybangiay
   dotnet run
   ```
3. **Truy cập hệ thống:**
   * **Storefront Khách hàng:** `http://localhost:5000` (hoặc port hiển thị trên terminal).
   * **Cổng Quản trị Admin:** `http://localhost:5000/Admin`.

---
*Biên bản được xuất tự động bởi Tech Lead Supervisor — DevAgent Orchestrator.*
