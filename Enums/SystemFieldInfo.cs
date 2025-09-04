namespace BIDashboardBackend.Enums
{
    public class SystemFieldInfo
    {
        public class SystemFieldProp
        {
            public string FieldName { get; set; }
            public string ExpectedType { get; set; }
        }



        public static Dictionary<SystemField, SystemFieldProp> SystemFieldDict = new Dictionary<SystemField, SystemFieldProp>()
        {
            { SystemField.Name, new SystemFieldProp { FieldName = "Name", ExpectedType = "string" } },
            { SystemField.Email, new SystemFieldProp { FieldName = "Email", ExpectedType = "string" } },
            { SystemField.Phone, new SystemFieldProp { FieldName = "Phone", ExpectedType = "string" } },
            { SystemField.Gender, new SystemFieldProp { FieldName = "Gender", ExpectedType = "string" } },
            { SystemField.BirthDate, new SystemFieldProp { FieldName = "BirthDate", ExpectedType = "string" } },
            { SystemField.Age, new SystemFieldProp { FieldName = "Age", ExpectedType = "string" } },
            { SystemField.CustomerId, new SystemFieldProp { FieldName = "CustomerId", ExpectedType = "string" } },
            { SystemField.OrderId, new SystemFieldProp { FieldName = "OrderId", ExpectedType = "string" } },
            { SystemField.OrderDate, new SystemFieldProp { FieldName = "OrderDate", ExpectedType = "string" } },
            { SystemField.OrderAmount, new SystemFieldProp { FieldName = "OrderAmount", ExpectedType = "string" } },
            { SystemField.OrderStatus, new SystemFieldProp { FieldName = "OrderStatus", ExpectedType = "string" } },
            { SystemField.Region, new SystemFieldProp { FieldName = "Region", ExpectedType = "string" } },
            { SystemField.ProductCategory, new SystemFieldProp { FieldName = "ProductCategory", ExpectedType = "string" } }
        };


    }

    
}
