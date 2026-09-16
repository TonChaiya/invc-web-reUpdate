using Invc.Core.Diagnostics;
using Invc.Core.Inventory;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Diagnostics;
using Invc.Infrastructure.Inventory;
using Invc.Core.Reorder;
using Invc.Infrastructure.Reorder;
using Invc.Core.PurchaseOrders;
using Invc.Infrastructure.PurchaseOrders;
using Invc.Core.Receipts;
using Invc.Infrastructure.Receipts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Invc.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers read-only data access for the production INV database.
    /// No schema initialisation, migration, or seeding is performed — ever.
    /// </summary>
    public static IServiceCollection AddInvcReadOnlyData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InvDatabaseOptions>(configuration.GetSection(InvDatabaseOptions.SectionName));
        services.AddSingleton<ISqlConnectionFactory, ReadOnlySqlConnectionFactory>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IReorderRepository, ReorderRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<INonPoReceiptRepository, NonPoReceiptRepository>();
        services.AddScoped<IDatabaseHealth, DatabaseHealth>();
        return services;
    }
}
