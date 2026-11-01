using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public static class ProductTypeRulesFactory
    {
        public static ProductTypeRules? Create(string? type, INotificationService ns, AppDbContext ctx)
        {
            return type switch
            {
                ProductType.Normal => new NormalProductRules(ns, ctx),
                ProductType.Seasonal => new SeasonalProductRules(ns, ctx),
                ProductType.Expirable => new ExpirableProductRules(ns, ctx),
                _ => null
            };
        }
    }
}
