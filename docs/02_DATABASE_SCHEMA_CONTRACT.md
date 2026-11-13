# HỢP ĐỒNG THIẾT KẾ CƠ SỞ DỮ LIỆU (DATABASE SCHEMA CONTRACT)
## Hệ Thống Quản Lý Bán Giày, Kho Hàng, Đổi Trả & Dòng Tiền (SQL Server 2022)

---

### 1. SƠ ĐỒ THỰC THỂ LIÊN KẾT (ENTITY RELATIONSHIP DIAGRAM)

```mermaid
erDiagram
    USERS ||--o{ ORDERS : "đặt hàng (nullable cho khách vãng lai)"
    VOUCHERS ||--o{ ORDERS : "áp dụng giảm giá"
    PRODUCTS ||--|{ PRODUCT_VARIANTS : "có nhiều biến thể size/màu"
    SUPPLIERS ||--|{ PURCHASE_ORDERS : "cung cấp hàng"
    SUPPLIERS ||--o{ SUPPLIER_PAYMENTS : "nhận thanh toán công nợ"
    PURCHASE_ORDERS ||--|{ PURCHASE_ORDER_ITEMS : "chứa chi tiết nhập"
    PURCHASE_ORDERS ||--o{ SUPPLIER_PAYMENTS : "thanh toán theo phiếu nhập"
    PRODUCT_VARIANTS ||--|{ PURCHASE_ORDER_ITEMS : "được nhập"
    ORDERS ||--|{ ORDER_ITEMS : "gồm các sản phẩm"
    ORDERS ||--o{ ORDER_RETURNS : "phát sinh đổi/trả hàng"
    ORDER_RETURNS ||--|{ ORDER_RETURN_ITEMS : "chi tiết sản phẩm đổi/trả"
    PRODUCT_VARIANTS ||--|{ ORDER_ITEMS : "được bán"
    PRODUCT_VARIANTS ||--|{ INVENTORY_TRANSACTIONS : "ghi vết biến động kho"
    ORDERS ||--o{ CASH_FLOW_LEDGER : "sinh phiếu thu / chi hoàn tiền"
    PURCHASE_ORDERS ||--o{ CASH_FLOW_LEDGER : "sinh phiếu chi tiền nhập"
    SUPPLIER_PAYMENTS ||--o| CASH_FLOW_LEDGER : "sinh phiếu chi trả nợ NCC"

    USERS {
        int id PK
        string username UK
        string email UK
        string password_hash
        string full_name
        string phone
        string role "Admin / Staff / Customer"
        boolean is_active
        datetime created_at
    }

    PRODUCTS {
        int id PK
        string sku UK
        string name
        string brand
        string category
        decimal retail_price
        decimal original_price
        string image_url
        double rating
        boolean is_new
        boolean is_best_seller
        boolean is_active
        datetime created_at
    }

    PRODUCT_VARIANTS {
        int id PK
        int product_id FK
        int size
        string color
        string variant_sku UK
        string image_url "Ảnh riêng theo màu (nullable)"
        decimal cost_price "Giá vốn bình quân hiện tại"
        int stock_quantity "Tồn kho vật lý thực tế (>= 0)"
        int reserved_quantity "Số lượng đang giữ cho đơn Pending (>= 0)"
        int min_stock_alert "Ngưỡng cảnh báo sắp hết"
        rowversion row_version "Optimistic Concurrency Token"
        datetime updated_at
    }

    VOUCHERS {
        int id PK
        string code UK
        string discount_type "PERCENT / FIXED_AMOUNT"
        decimal discount_value
        decimal min_order_amount
        decimal max_discount_amount "Trần giảm giá (nullable)"
        int usage_limit
        int used_count
        datetime start_at
        datetime end_at
        boolean is_active
    }

    SUPPLIERS {
        int id PK
        string code UK
        string name
        string phone
        string email
        string address
        decimal current_debt "Công nợ phải trả NCC (>= 0)"
    }

    PURCHASE_ORDERS {
        int id PK
        string po_code UK "Mã phiếu nhập (PN-YYYYMMDD-XXX)"
        int supplier_id FK
        decimal total_amount "Tổng tiền hàng nhập"
        decimal paid_amount "Số tiền đã thanh toán"
        string payment_status "Unpaid / Partial / Paid"
        string status "Draft / Received / Cancelled"
        datetime order_date
        datetime received_date
    }

    PURCHASE_ORDER_ITEMS {
        int id PK
        int purchase_order_id FK
        int product_variant_id FK
        int quantity "Số lượng nhập"
        decimal unit_cost "Giá nhập 1 đôi"
        decimal total_cost "Thành tiền nhập"
    }

    SUPPLIER_PAYMENTS {
        int id PK
        string payment_code UK "Mã chi trả nợ (TTNCC-YYYYMMDD-XXX)"
        int supplier_id FK
        int purchase_order_id FK "Nullable nếu trả gộp công nợ"
        decimal amount
        string payment_method "CASH / BANK_TRANSFER"
        datetime paid_at
        string note
    }

    ORDERS {
        int id PK
        string order_code UK "Mã đơn hàng (DH-YYYYMMDD-XXX)"
        int user_id FK "Nullable cho khách mua không cần đăng nhập"
        int voucher_id FK "Nullable"
        string voucher_code "Snapshot mã giảm giá"
        string customer_name
        string customer_phone
        string shipping_address
        decimal subtotal "Tiền hàng"
        decimal discount_amount "Giảm giá voucher"
        decimal shipping_fee "Phí ship"
        decimal total_amount "Khách cần trả (VAT = 0)"
        string payment_method "COD / QR_TRANSFER / MOCK_CARD"
        string payment_status "Pending / Paid / Refunded"
        string order_status "Pending / Confirmed / Shipping / Completed / Cancelled"
        rowversion row_version
        datetime created_at
    }

    ORDER_ITEMS {
        int id PK
        int order_id FK
        int product_variant_id FK
        string product_name
        int size
        string color
        decimal unit_price "Giá bán thực tế"
        decimal unit_cost "Snapshot giá vốn tại thời điểm bán"
        int quantity
        decimal total_price
    }

    ORDER_RETURNS {
        int id PK
        string return_code UK "Mã phiếu đổi/trả (RT-YYYYMMDD-XXX)"
        int order_id FK
        string return_type "REFUND / EXCHANGE"
        string reason
        decimal refund_amount "Số tiền hoàn trả khách (nếu REFUND)"
        string status "Pending / Approved / Completed / Rejected"
        datetime created_at
        datetime completed_at
    }

    ORDER_RETURN_ITEMS {
        int id PK
        int order_return_id FK
        int original_variant_id FK
        int exchange_variant_id FK "Nullable nếu chỉ hoàn tiền"
        int quantity
        decimal refund_unit_price
        boolean is_resellable "Hàng trả về có bán lại được không"
    }

    INVENTORY_TRANSACTIONS {
        int id PK
        string tx_code UK "Mã giao dịch kho"
        int product_variant_id FK
        string tx_type "IMPORT / EXPORT_SALE / ADJUSTMENT / RETURN / EXCHANGE_OUT"
        int quantity_change "+ hoặc - số lượng"
        int quantity_before "Tồn trước"
        int quantity_after "Tồn sau"
        string reference_type "PurchaseOrder / Order / OrderReturn / Manual"
        int reference_id "ID chứng từ gốc"
        int created_by_user_id "Nullable"
        datetime created_at
        string note
    }

    CASH_FLOW_LEDGER {
        int id PK
        string voucher_code UK "Mã phiếu thu/chi (PT-xxx / PC-xxx)"
        string voucher_type "RECEIPT (Thu) / PAYMENT (Chi)"
        string category "SALE_REVENUE / SUPPLIER_PAYMENT / CUSTOMER_REFUND / OPERATIONAL_COST"
        decimal amount "Số tiền thực thu hoặc thực chi"
        string payment_method "CASH / BANK_TRANSFER"
        string reference_type "Order / PurchaseOrder / SupplierPayment / OrderReturn / Other"
        int reference_id "ID chứng từ gốc"
        int created_by_user_id "Nullable"
        datetime created_at
        string note
    }
```

---

### 2. CHI TIẾT CÁC BẢNG DỮ LIỆU & RÀNG BUỘC (DATA DICTIONARY)

#### 2.1. Nhóm Tài Khoản & Khuyến Mãi
1. **`Users`**: Lưu tài khoản đăng nhập cho `Admin`, `Staff`, `Customer`. Mật khẩu băm bằng `PasswordHasher` (PBKDF2/Argon2), không lưu plaintext.
2. **`Vouchers`**: Quản lý mã giảm giá động (`PERCENT` hoặc `FIXED_AMOUNT`), có kiểm tra thời hạn hiệu lực (`start_at`..`end_at`), ngưỡng đơn tối thiểu (`min_order_amount`), giới hạn lượt dùng (`used_count < usage_limit`).

#### 2.2. Nhóm Sản Phẩm & Biến Thể Kho (`Products`, `ProductVariants`)
* `ProductVariants` quản lý tồn kho theo cặp `(product_id, size, color)` (Unique Index).
* **Tồn kho khả dụng (Available Stock)**:
  $$\text{AvailableQuantity} = \text{stock\_quantity} - \text{reserved\_quantity}$$
* **Ràng buộc SQL Server**:
  * `CHECK (stock_quantity >= 0)`
  * `CHECK (reserved_quantity >= 0)`
  * `CHECK (stock_quantity >= reserved_quantity)`
  * `row_version ROWVERSION` chống ghi đè đồng thời (Optimistic Concurrency).

#### 2.3. Nhóm Nhập Hàng & Công Nợ Nhà Cung Cấp
* **`Suppliers`**: Lưu `current_debt` (`CHECK (current_debt >= 0)`).
* **`PurchaseOrders`** khi chuyển sang `Received`:
  * Tăng `stock_quantity`, cập nhật `cost_price` theo giá vốn bình quân gia quyền.
  * Ghi `InventoryTransactions` loại `IMPORT`.
  * Cộng dồn công nợ: `current_debt += (total_amount - paid_amount)`.
  * Nếu `paid_amount > 0`: ghi `CashFlowLedger` loại `PAYMENT`, category `SUPPLIER_PAYMENT`.
* **`SupplierPayments`**: Ghi nhận các đợt trả nợ bổ sung cho NCC, tự động giảm `Suppliers.current_debt`, cập nhật `PurchaseOrders.paid_amount` / `payment_status` và sinh phiếu chi `CashFlowLedger`.

#### 2.4. Nhóm Bán Hàng, Đổi Trả & Dòng Tiền
* **`Orders` & `OrderItems`**:
  * Lưu `OrderItems.unit_cost` (snapshot giá vốn tại thời điểm bán) để tính chính xác **Lợi nhuận gộp**:
    $$\text{Lợi nhuận gộp} = \sum (\text{unit\_price} - \text{unit\_cost}) \times \text{quantity} - \text{discount\_amount}$$
* **`OrderReturns` & `OrderReturnItems`**: Hỗ trợ hoàn tiền (`REFUND`) và đổi size/màu (`EXCHANGE`).
* **`InventoryTransactions` & `CashFlowLedger`**: Sổ cái bất biến (Append-only). Mọi biến động kho hoặc dòng tiền đều có `reference_type` và `reference_id` truy vết ngược về chứng từ gốc.
