using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderReturn> OrderReturns => Set<OrderReturn>();
    public DbSet<OrderReturnItem> OrderReturnItems => Set<OrderReturnItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<CashFlowLedger> CashFlowLedger => Set<CashFlowLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(250).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(100).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
            e.Property(x => x.RetailPrice).HasPrecision(18, 2);
            e.Property(x => x.OriginalPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.HasIndex(x => x.VariantSku).IsUnique();
            e.HasIndex(x => new { x.ProductId, x.Size, x.Color }).IsUnique();
            e.Property(x => x.VariantSku).HasMaxLength(80).IsRequired();
            e.Property(x => x.Color).HasMaxLength(80).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.CostPrice).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProductVariants_StockQuantity_NonNegative", "[StockQuantity] >= 0");
                t.HasCheckConstraint("CK_ProductVariants_ReservedQuantity_NonNegative", "[ReservedQuantity] >= 0");
                t.HasCheckConstraint("CK_ProductVariants_StockAtLeastReserved", "[StockQuantity] >= [ReservedQuantity]");
            });
            e.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Voucher>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.DiscountType).HasMaxLength(30).IsRequired();
            e.Property(x => x.DiscountValue).HasPrecision(18, 2);
            e.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
            e.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.CurrentDebt).HasPrecision(18, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_Suppliers_CurrentDebt_NonNegative", "[CurrentDebt] >= 0"));
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(x => x.PoCode).IsUnique();
            e.Property(x => x.PoCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.PaymentStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.PaidAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Supplier).WithMany(x => x.PurchaseOrders).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrderItem>(e =>
        {
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.TotalCost).HasPrecision(18, 2);
            e.HasOne(x => x.PurchaseOrder).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProductVariant).WithMany(x => x.PurchaseOrderItems).HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierPayment>(e =>
        {
            e.HasIndex(x => x.PaymentCode).IsUnique();
            e.Property(x => x.PaymentCode).HasMaxLength(60).IsRequired();
            e.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.Supplier).WithMany(x => x.SupplierPayments).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrder).WithMany(x => x.SupplierPayments).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderCode).IsUnique();
            e.Property(x => x.OrderCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.VoucherCode).HasMaxLength(50);
            e.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CustomerPhone).HasMaxLength(30).IsRequired();
            e.Property(x => x.ShippingAddress).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            e.Property(x => x.PaymentStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.OrderStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.User).WithMany(x => x.Orders).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Voucher).WithMany(x => x.Orders).HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            e.Property(x => x.Color).HasMaxLength(80).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.TotalPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProductVariant).WithMany(x => x.OrderItems).HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderReturn>(e =>
        {
            e.HasIndex(x => x.ReturnCode).IsUnique();
            e.Property(x => x.ReturnCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.ReturnType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.RefundAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.HasOne(x => x.Order).WithMany(x => x.Returns).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderReturnItem>(e =>
        {
            e.Property(x => x.RefundUnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.OrderReturn).WithMany(x => x.Items).HasForeignKey(x => x.OrderReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OriginalVariant).WithMany(x => x.OriginalReturnItems).HasForeignKey(x => x.OriginalVariantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ExchangeVariant).WithMany(x => x.ExchangeReturnItems).HasForeignKey(x => x.ExchangeVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.HasIndex(x => x.TxCode).IsUnique();
            e.Property(x => x.TxCode).HasMaxLength(60).IsRequired();
            e.Property(x => x.TxType).HasMaxLength(40).IsRequired();
            e.Property(x => x.ReferenceType).HasMaxLength(60).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.ProductVariant).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashFlowLedger>(e =>
        {
            e.HasIndex(x => x.VoucherCode).IsUnique();
            e.Property(x => x.VoucherCode).HasMaxLength(60).IsRequired();
            e.Property(x => x.VoucherType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Category).HasMaxLength(50).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            e.Property(x => x.ReferenceType).HasMaxLength(60).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.Order).WithMany(x => x.CashFlowEntries).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SupplierPayment).WithMany().HasForeignKey(x => x.SupplierPaymentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
