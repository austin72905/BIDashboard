namespace BIDashboardBackend.Enums
{
    // 定義 「原始資料欄位」（系統承認的標準欄位）。
    // 目的是解決「使用者上傳的 CSV 欄位名五花八門」的問題。
    // 在 Mapping 表裡，使用者把 CSV 欄位 "CustName" → SystemField.Name
    public enum SystemField
    {
        None=-1, // 未映射
        Name,
        Email,
        Phone,
        Gender,
        BirthDate,
        Age,
        CustomerId,
        OrderId,
        OrderDate,
        OrderAmount,
        OrderStatus,
        Region,
        ProductCategory
    }
}
