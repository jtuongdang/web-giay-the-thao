# HỢP ĐỒNG GIAO DIỆN API (RESTFUL API CONTRACT SPECIFICATION)
## Giao Tiếp Độc Lập Giữa Frontend (Client) & Backend (API Service)

---

### 1. QUY CHUẨN GIAO TIẾP CHUNG
- **Định dạng dữ liệu**: Toàn bộ Request và Response đều sử dụng `application/json` (UTF-8).
- **Cấu trúc phản hồi chuẩn (Standard JSON Response Envelope)**:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Thao tác thành công",
  "data": { ... },
  "errors": null,
  "timestamp": "2026-11-13T01:00:00Z"
}
```

---

### 2. BỘ API DÀNH CHO KHÁCH HÀNG (STOREFRONT APIs)

#### 2.1. Tra cứu danh sách sản phẩm
- **Endpoint**: `GET /api/v1/products`
- **Query Params**:
  - `brand`: Nike, Adidas, Jordan, Puma
  - `size`: 39, 40, 41, 42, 43, 44
  - `search`: Tên giày
  - `minPrice`, `maxPrice`: Khoảng giá lọc
  - `pageIndex`, `pageSize`: Phân trang
- **Mô tả**: Trả về danh sách thẻ giày kèm thông tin tồn kho tổng quan.

#### 2.2. Xem chi tiết giày & biến thể size tồn kho
- **Endpoint**: `GET /api/v1/products/{id}`
- **Mẫu dữ liệu trả về**:
```json
{
  "id": 1,
  "name": "Nike Air Jordan 1 High OG Chicago",
  "brand": "Jordan",
  "category": "Bóng rổ",
  "retailPrice": 5200000,
  "originalPrice": 5800000,
  "imageUrl": "https://images.unsplash.com/...",
  "variants": [
    { "variantId": 101, "size": 39, "inStock": true, "availableQuantity": 12 },
    { "variantId": 102, "size": 40, "inStock": true, "availableQuantity": 5 },
    { "variantId": 103, "size": 41, "inStock": false, "availableQuantity": 0 }
  ]
}
```

#### 2.3. Đặt hàng (Checkout)
- **Endpoint**: `POST /api/v1/orders`
- **Body**:
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
- **Response**: Trả về `orderCode`, số tiền cần thanh toán (không thuế), và dữ liệu QR thanh toán mô phỏng.

#### 2.4. Mô phỏng xác nhận thanh toán (Mock Payment Simulator)
- **Endpoint**: `POST /api/v1/payments/mock-simulate`
- **Body**:
```json
{
  "orderCode": "DH-20261113-001",
  "status": "SUCCESS" // hoặc "FAILED"
}
```

---

### 3. BỘ API QUẢN TRỊ KHO & DÒNG TIỀN (ADMIN / BACKOFFICE APIs)

#### 3.1. Quản lý kho hàng & cảnh báo tồn kho
- `GET /api/v1/admin/inventory`: Danh sách tất cả biến thể, số lượng tồn kho vật lý, giá vốn hiện tại, cờ cảnh báo `isLowStock` (khi tồn $\le$ `minStockAlert`).
- `GET /api/v1/admin/inventory/transactions`: Lịch sử biến động xuất nhập tồn kho.

#### 3.2. Nhập hàng từ Nhà cung cấp (Purchase Orders)
- `POST /api/v1/admin/purchase-orders`: Tạo phiếu nhập hàng (Chọn Nhà cung cấp, danh sách giày, size, số lượng, giá vốn nhập).
- `POST /api/v1/admin/purchase-orders/{id}/receive`:
  - **Hành động**: Xác nhận hàng thực tế đã về kho.
  - **Hậu quả hệ thống**:
    1. Tăng tồn kho `stock_quantity` của từng size.
    2. Ghi nhật ký vào `InventoryTransactions`.
    3. Nếu có thanh toán ngay, tự động sinh phiếu chi trong `CashFlowLedger`.

#### 3.3. Xử lý đơn hàng & xuất kho
- `GET /api/v1/admin/orders`: Quản lý danh sách đơn hàng.
- `PATCH /api/v1/admin/orders/{id}/status`: Đổi trạng thái (`Confirmed` -> Trừ kho; `Completed` -> Ghi nhận dòng tiền thu nếu là COD; `Cancelled` -> Hoàn kho).

#### 3.4. Báo cáo Dòng tiền & Tài chính kinh doanh (Cash Flow & Financial Analytics)
- `GET /api/v1/admin/finance/summary`:
  - Trả về KPI tổng hợp:
    - **Tổng Doanh thu (Total Revenue)**
    - **Tổng Giá vốn hàng bán (Total COGS)**
    - **Lợi nhuận gộp (Gross Profit = Revenue - COGS)**
    - **Tổng Tiền thực thu (Cash Inflow)**
    - **Tổng Tiền thực chi (Cash Outflow)**
    - **Dòng tiền thuần (Net Cash Flow = Cash In - Cash Out)**
- `GET /api/v1/admin/finance/cash-ledger`: Sổ quỹ chi tiết toàn bộ các phiếu Thu và phiếu Chi phát sinh.
- `GET /api/v1/admin/finance/charts`: Dữ liệu chuỗi thời gian (Time-series) vẽ biểu đồ đường/cột cho Frontend Admin Portal (Doanh thu theo ngày, Lãi gộp theo ngày, Dòng tiền thu/chi).
