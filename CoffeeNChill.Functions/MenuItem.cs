using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Functions
{
    public class MenuItem : ITableEntity
    {
        // Business properties
        public string Name { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public bool IsAvailable { get; set; }

        // Required by Azure Table Storage
        public string PartitionKey { get; set; }  // e.g., "Hot Drinks"
        public string RowKey { get; set; }        // e.g., "COF-001"
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}