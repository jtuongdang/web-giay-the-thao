namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Aggregated sales, profitability, cash flow, and supplier payable totals.</summary>
public sealed record FinanceSummaryDto
{
    /// <summary>Net sales revenue.</summary>
    public decimal NetRevenue { get; init; }

    /// <summary>Total cost of goods sold.</summary>
    public decimal TotalCogs { get; init; }

    /// <summary>Gross profit after cost of goods sold.</summary>
    public decimal GrossProfit { get; init; }

    /// <summary>Total cash received.</summary>
    public decimal CashInflow { get; init; }

    /// <summary>Total cash paid out.</summary>
    public decimal CashOutflow { get; init; }

    /// <summary>Cash inflow minus cash outflow.</summary>
    public decimal NetCashFlow { get; init; }

    /// <summary>Total outstanding supplier debt.</summary>
    public decimal SupplierDebt { get; init; }

    /// <summary>Start of the reporting period in UTC, inclusive.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>End of the reporting period in UTC, inclusive.</summary>
    public DateTimeOffset PeriodEnd { get; init; }
}

/// <summary>A cash receipt or expenditure recorded in the cash ledger.</summary>
public sealed record CashLedgerEntryDto
{
    /// <summary>Cash ledger entry identifier.</summary>
    public int Id { get; init; }

    /// <summary>Entry direction, such as INFLOW or OUTFLOW.</summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>Entry category, such as operational cost or supplier payment.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Transaction amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Optional source type, such as an order or purchase order.</summary>
    public string? ReferenceType { get; init; }

    /// <summary>Optional source record identifier.</summary>
    public int? ReferenceId { get; init; }

    /// <summary>Entry description or note.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Transaction time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>User who recorded the entry.</summary>
    public string? CreatedBy { get; init; }
}

/// <summary>Manual operational income or expense submitted to the cash ledger.</summary>
public sealed record CreateCashLedgerRequest
{
    /// <summary>Entry direction, such as INFLOW or OUTFLOW.</summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>Entry category, such as OPERATIONAL_COST.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Transaction amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Description or reason for the entry.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Optional external reference.</summary>
    public string? Reference { get; init; }
}

/// <summary>One value in a dated finance chart series.</summary>
public sealed record FinanceChartPointDto
{
    /// <summary>Time bucket represented by this point, in UTC.</summary>
    public DateTimeOffset Period { get; init; }

    /// <summary>Metric value for the time bucket.</summary>
    public decimal Value { get; init; }
}

/// <summary>Named time series for a finance chart metric.</summary>
public sealed record FinanceChartSeriesDto
{
    /// <summary>Series label, such as Revenue or CashInflow.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Metric represented by the series.</summary>
    public string Metric { get; init; } = string.Empty;

    /// <summary>Values grouped by the requested day, week, or month period.</summary>
    public IReadOnlyList<FinanceChartPointDto> DataPoints { get; init; } =
        Array.Empty<FinanceChartPointDto>();
}
