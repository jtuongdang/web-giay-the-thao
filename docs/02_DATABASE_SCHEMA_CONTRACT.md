# HỢP ĐỒNG THIẾT KẾ CƠ SỞ DỮ LIỆU (DATABASE SCHEMA CONTRACT)
## Hệ Thống Quản Lý Bán Giày, Kho Hàng & Dòng Tiền Toàn Diện

---

### 1. SƠ ĐỒ THỰC THỂ LIÊN KẾT (ENTITY RELATIONSHIP DIAGRAM)

```mermaid
erDiagram
    PRODUCTS ||--|{ PRODUCT_VARIANTS : "có nhiều biến thể size/màu"
    SUPPLIERS ||--|{ PURCHASE_ORDERS : "cung cấp hàng"
    PURCHASE_ORDERS ||--|{ PURCHASE_ORDER_ITEMS : "chứa chi tiết nhập"
    PRODUCT_VARIANTS ||--|{ PURCHASE_ORDER_ITEMS : "được nhập"
    ORDERS ||--|{ ORDER_ITEMS : "gồm các sản phẩm"
    PRODUCT_VARIANTS ||--|{ ORDER_ITEMS : "được bán"
    PRODUCT_VARIANTS ||--|{ INVENTORY_TRANSACTIONS : "ghi vết biến động kho"
    ORDERS ||--o| CASH_FLOW_LEDGER : "sinh phiếu thu tiền"
    PURCHASE_ORDERS ||--o| CASH_FLOW_LEDGER : "sinh phiếu chi tiền nhập"

    PRODUCTS {
        int id PK
        string sku UK
        string name
        string brand
        string category
        decimal retail_price
        decimal original_price
        string image_url
        boolean is_active
        datetime created_at
    }

    PRODUCT_VARIANTS {
        int id PK
        int product_id FK
        int size
        string color
        string variant_sku UK
        decimal cost_price "Giá vốn hiện tại"
        int stock_quantity "Tồn kho thực tế"
        int min_stock_alert "Mức báo động sắp hết"
        datetime updated_at
    }

    SUPPLIERS {
        int id PK
        string code UK
        string name
        string phone
        string email
        string address
        decimal current_debt "Công nợ phải trả"
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

    ORDERS {
        int id PK
        string order_code UK "Mã đơn hàng (DH-YYYYMMDD-XXX)"
        string customer_name
        string customer_phone
        string shipping_address
        decimal subtotal "Tiền hàng"
        decimal discount_amount "Giảm giá voucher"
        decimal shipping_fee "Phí ship"
        decimal total_amount "Khách cần trả (KHÔNG THUẾ)"
        string payment_method "COD / QR_TRANSFER / MOCK_CARD"
        string payment_status "Pending / Paid / Refunded"
        string order_status "Pending / Confirmed / Shipping / Completed / Cancelled"
        datetime created_at
    }

    ORDER_ITEMS {
        int id PK
        int order_id FK
        int product_variant_id FK
        string product_name
        int size
        decimal unit_price "Giá bán thực tế"
        decimal unit_cost "Giá vốn tại thời điểm bán (để tính lãi chuẩn)"
        int quantity
        decimal total_price
    }

    INVENTORY_TRANSACTIONS {
        int id PK
        string tx_code UK "Mã giao dịch kho"
        int product_variant_id FK
        string tx_type "IMPORT / EXPORT_SALE / ADJUSTMENT / RETURN"
        int quantity_change "+ hoặc - số lượng"
        int quantity_before "Tồn trước"
        int quantity_after "Tồn sau"
        string reference_type "PurchaseOrder / Order / Manual"
        int reference_id "ID chứng từ gốc"
        datetime created_at
        string note
    }

    CASH_FLOW_LEDGER {
        int id PK
        string voucher_code UK "Mã phiếu thu/chi (PT-xxx / PC-xxx)"
        string voucher_type "RECEIPT (Thu) / PAYMENT (Chi)"
        string category "SALE_REVENUE / SUPPLIER_PAYMENT / OPERATIONAL_COST"
        decimal amount "Số tiền thực thu hoặc thực chi"
        string payment_method "CASH / BANK_TRANSFER"
        string reference_type "Order / PurchaseOrder / Other"
        int reference_id "ID đơn hoặc phiếu nhập"
        datetime created_at
        string note
    }
```

---

### 2. CHI TIẾT CÁC BẢNG DỮ LIỆU & RÀNG BUỘC (DATA DICTIONARY)

#### 2.1. Nhóm Quản Lý Sản Phẩm & Biến Thể Kho
1. **`Products`**: Lưu thông tin gốc của đôi giày (tên, hãng, danh mục, giá niêm yết, ảnh đại diện).
2. **`ProductVariants`**:
   - Quản lý chính xác từng **Size** (ví dụ: 39, 40, 41, 42, 43, 44).
   - `stock_quantity`: Số lượng giày vật lý hiện có trong kho.
   - `cost_price`: Giá vốn bình quân/giá nhập gần nhất của biến thể này.
   - Ràng buộc: `CHECK (stock_quantity >= 0)` (Ngăn chặn kho bị âm).

#### 2.2. Nhóm Quản Lý Nhập Hàng (Purchase Orders)
1. **`Suppliers`**: Danh bạ nhà cung cấp giày, xưởng sản xuất, đầu mối phân phối.
2. **`PurchaseOrders`** & **`PurchaseOrderItems`**:
   - Khi tạo phiếu nhập và bấm **"Xác nhận nhập kho" (Received)**:
     - Tự động tăng `stock_quantity` trong bảng `ProductVariants`.
     - Tự động ghi 1 dòng vào bảng `InventoryTransactions` với loại `IMPORT`.
     - Nếu đã thanh toán (`paid_amount > 0`): Tự động sinh 1 phiếu chi trong bảng `CashFlowLedger` với loại `PAYMENT` (Chi tiền nhập hàng).

#### 2.3. Nhóm Bán Hàng & Đơn Hàng (Orders)
1. **`Orders`** & **`OrderItems`**:
   - Bỏ hoàn toàn thuế (`Tax = 0`).
   - `OrderItems.unit_cost`: **Điểm mấu chốt tài chính!** Lưu snapshot giá vốn của đôi giày tại đúng thời điểm xuất bán. Nhờ đó, dù sau này giá nhập biến động, báo cáo lãi gộp của đơn hàng cũ vẫn chính xác 100%:
     $$\text{Lợi nhuận gộp đơn hàng} = \sum (\text{unit\_price} - \text{unit\_cost}) \times \text{quantity} - \text{discount\_amount}$$

#### 2.4. Nhóm Sổ Kho & Dòng Tiền (Core Operations & Financials)
1. **`InventoryTransactions` (Sổ cái kho)**:
   - Ghi nhận lịch sử bất biến (Immutable log). Bất kỳ khi nào kho tăng/giảm đều phải có 1 dòng ghi nhận ai làm, vì lý do gì, trước khi đổi là bao nhiêu, sau khi đổi là bao nhiêu.
2. **`CashFlowLedger` (Sổ quỹ dòng tiền)**:
   - Quản lý toàn bộ tiền thực vào/ra của cửa hàng:
     - **Thu (Receipt)**: Khách trả tiền đơn hàng (COD đối soát thành công, hoặc chuyển khoản thành công).
     - **Chi (Payment)**: Tiền trả cho nhà cung cấp khi nhập hàng, tiền chi phí vận hành (thuê mặt bằng, bao bì, đóng gói).
   - Công thức dòng tiền ròng (Net Cash Flow):
     $$\text{Dòng tiền thuần (Net Cash Flow)} = \sum \text{Tiền thu thực tế} - \sum \text{Tiền chi thực tế}$$
