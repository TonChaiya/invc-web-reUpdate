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
using Invc.Core.Dashboard;
using Invc.Infrastructure.Dashboard;
using Invc.Core.Borrow;
using Invc.Infrastructure.AppData;
using Invc.Infrastructure.Borrow;
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
        services.AddScoped<IDashboardAnalyticsRepository, DashboardAnalyticsRepository>();
        services.AddScoped<DashboardService>();
        services.AddScoped<IDatabaseHealth, DatabaseHealth>();
        services.AddScoped<IBorrowSourceRepository, BorrowSourceRepository>();   // INV, SELECT only
        services.AddScoped<IBorrowReceiptAdvisoryRepository, BorrowReceiptAdvisoryRepository>();   // INV, SELECT only (advisory hints)
        return services;
    }

    /// <summary>
    /// Registers the APPLICATION-OWNED MySQL database (`invc_web`) used by the Borrow module as a writable mirror/workflow store.
    /// Separate provider, options and factory from INV; schema is created by the versioned scripts under db/mysql (never at startup).
    /// </summary>
    public static IServiceCollection AddInvcAppData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppDatabaseOptions>(configuration.GetSection(AppDatabaseOptions.SectionName));
        services.AddSingleton<IAppDbConnectionFactory, MySqlAppDbConnectionFactory>();
        services.AddScoped<IBorrowMirrorRepository, BorrowMirrorRepository>();
        services.AddScoped<IBorrowReturnRepository, BorrowReturnRepository>();
        services.AddScoped<IAppDatabaseHealth, AppDatabaseHealth>();
        services.AddScoped<BorrowSyncService>();
        return services;
    }
}
