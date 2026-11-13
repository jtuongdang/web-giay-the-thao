# HỢP ĐỒNG GIAO DIỆN API (RESTFUL API CONTRACT SPECIFICATION)
## Giao Tiếp Độc Lập Giữa Frontend (Storefront / Admin) & Backend (API Service)

---

### 1. QUY CHUẨN GIAO TIẾP CHUNG
* **Định dạng dữ liệu**: Toàn bộ Request và Response sử dụng `application/json` (UTF-8).
* **Phân quyền**:
  * Các endpoint `/api/v1/products*`, `/api/v1/vouchers/validate`, `POST /api/v1/orders` (Storefront): Public (hoặc gắn `userId` nếu có JWT).
  * Các endpoint `/api/v1/admin/*` và `POST /api/v1/payments/mock-simulate`: Bắt buộc JWT Bearer / Cookie với role `Admin` hoặc `Staff`.
* **Cấu trúc phản hồi chuẩn (`ApiResponse<T>`)**:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Thao tác thành công",
  "data": {},
  "errors": null,
  "timestamp": "2026-11-13T10:20:00Z"
}
```

---

### 2. BỘ API XÁC THỰC (AUTH APIs)
* `POST /api/v1/auth/login`: Đăng nhập bằng `username`/`email` + `password`, trả về JWT token + thông tin user (`role`).
* `POST /api/v1/auth/register`: Đăng ký tài khoản `Customer`.
* `GET /api/v1/auth/me` `[Authorize]`: Lấy thông tin phiên đăng nhập hiện tại.

---

### 3. BỘ API KHÁCH HÀNG (STOREFRONT APIs)

#### 3.1. Tra cứu danh sách sản phẩm
* `GET /api/v1/products`
* **Query Params**: `brand`, `size`, `color`, `search`, `minPrice`, `maxPrice`, `pageIndex`, `pageSize`.

#### 3.2. Xem chi tiết giày & biến thể size/màu tồn kho
* `GET /api/v1/products/{id}`
* **Mẫu `data` trả về**:
```json
{
  "id": 1,
  "sku": "NIKE-AJ1-CHI",
  "name": "Nike Air Jordan 1 High OG Chicago",
  "brand": "Jordan",
  "category": "Bóng rổ",
  "retailPrice": 5200000,
  "originalPrice": 5800000,
  "imageUrl": "https://images.unsplash.com/...",
  "variants": [
    {
      "variantId": 101,
      "size": 39,
      "color": "Chicago Red/White",
      "imageUrl": null,
      "inStock": true,
      "availableQuantity": 12
    },
    {
      "variantId": 102,
      "size": 41,
      "color": "Chicago Red/White",
      "imageUrl": null,
      "inStock": false,
      "availableQuantity": 0
    }
  ]
}
```

#### 3.3. Kiểm tra mã giảm giá (Voucher Validation)
* `POST /api/v1/vouchers/validate`
* **Body**: `{ "code": "SNEAKER10", "subtotal": 5200000 }`
* **Response**: Trả về `isValid`, `discountAmount`, `finalSubtotal`, `message`.

#### 3.4. Đặt hàng (Checkout & Stock Reservation)
* `POST /api/v1/orders`
* **Body**:
```json
{
  "customerName": "Đặng Gia Tường",
  "customerPhone": "0901234567",
  "shippingAddress": "123 Nguyễn Huệ, Quận 1, TP.HCM",
  "voucherCode": "SNEAKER10",
  "paymentMethod": "QR_TRANSFER",
  "items": [
    { "variantId": 101, "quantity": 1 }
  ]
}
```
* **Hành vi**: Giữ chỗ tồn kho (`reserved_quantity += quantity`) trong transaction. Trả về `orderCode`, `totalAmount` (VAT = 0), và dữ liệu QR mô phỏng nếu `paymentMethod == "QR_TRANSFER"`.

#### 3.5. Mô phỏng xác nhận thanh toán `[Authorize(Roles = "Admin,Staff")]`
* `POST /api/v1/payments/mock-simulate`
* **Body**: `{ "orderCode": "DH-20261113-001", "status": "SUCCESS" }`

---

### 4. BỘ API QUẢN TRỊ KHO, ĐỔI TRẢ & DÒNG TIỀN (`[Authorize(Roles = "Admin,Staff")]`)

#### 4.1. Quản lý kho & cảnh báo tồn kho
* `GET /api/v1/admin/inventory`: Danh sách biến thể kèm `stockQuantity`, `reservedQuantity`, `availableQuantity`, `costPrice`, `isLowStock`.
* `GET /api/v1/admin/inventory/transactions`: Lịch sử sổ cái kho (`InventoryTransactions`).
* `POST /api/v1/admin/inventory/adjust`: Kiểm kê/điều chỉnh tồn kho thủ công (ghi log `ADJUSTMENT`).

#### 4.2. Nhà cung cấp, Nhập hàng & Trả nợ (PO & Accounts Payable)
* `GET /api/v1/admin/suppliers` & `POST /api/v1/admin/suppliers`: Quản lý danh bạ NCC và xem `currentDebt`.
* `POST /api/v1/admin/suppliers/{id}/payments`: Ghi nhận trả nợ NCC -> giảm `currentDebt`, cập nhật PO và sinh phiếu chi `CashFlowLedger`.
* `POST /api/v1/admin/purchase-orders`: Tạo phiếu nhập hàng.
* `POST /api/v1/admin/purchase-orders/{id}/receive`: Xác nhận nhập kho -> tăng `stock_quantity`, tính lại giá vốn bình quân, ghi sổ kho `IMPORT`, cập nhật nợ/phiếu chi.

#### 4.3. Xử lý đơn hàng & Đổi trả (Orders & RMA)
* `GET /api/v1/admin/orders`: Danh sách đơn hàng (lọc theo trạng thái, ngày, SĐT).
* `PATCH /api/v1/admin/orders/{id}/status`: Chuyển trạng thái theo State Machine (`Confirmed` / `Shipping` / `Completed` / `Cancelled`).
* `POST /api/v1/admin/returns`: Tạo & xử lý yêu cầu Đổi size (`EXCHANGE`) hoặc Hoàn tiền (`REFUND`) cho đơn `Completed`.

#### 4.4. Quản lý Voucher
* `GET /api/v1/admin/vouchers`, `POST /api/v1/admin/vouchers`, `PATCH /api/v1/admin/vouchers/{id}`: Tạo, sửa, bật/tắt mã khuyến mãi.

#### 4.5. Báo cáo Dòng tiền & Tài chính (`Cash Flow & Financial Analytics`)
* `GET /api/v1/admin/finance/summary`: Tổng Doanh thu thuần, Tổng COGS, Lợi nhuận gộp, Tổng Tiền thực thu (`CashInflow`), Tổng Tiền thực chi (`CashOutflow`), Dòng tiền thuần (`NetCashFlow`), Tổng Công nợ NCC.
* `GET /api/v1/admin/finance/cash-ledger`: Sổ quỹ chi tiết các phiếu Thu/Chi (`CashFlowLedger`).
* `POST /api/v1/admin/finance/cash-ledger`: Tạo phiếu thu/chi vận hành thủ công (`OPERATIONAL_COST`, v.v.).
* `GET /api/v1/admin/finance/charts`: Chuỗi thời gian (Doanh thu, Lãi gộp, Thu/Chi theo ngày/tuần/tháng).
