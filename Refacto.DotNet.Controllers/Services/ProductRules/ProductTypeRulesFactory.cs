using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.Services.ProductRules
{
    public static class ProductTypeRulesFactory
    {
        public static ProductTypeRules? Create(string? type, INotificationService notificationService, AppDbContext dbContext)
        {
            return type switch
            {
                ProductType.Normal => new NormalProductRules(notificationService, dbContext),
                ProductType.Seasonal => new SeasonalProductRules(notificationService, dbContext),
                ProductType.Expirable => new ExpirableProductRules(notificationService, dbContext),
                _ => null
            };
        }
    }
}
