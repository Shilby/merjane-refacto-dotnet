using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.Services.ProductRules
{
    public class NormalProductRules : ProductTypeRules
    {
        public NormalProductRules(INotificationService notificationService, AppDbContext dbContext) : base(notificationService, dbContext)
        {
        }

        public override void Process(Product product, DateTime now)
        {
            if (product.Available > 0)
            {
                Sell(product);
            }
            else if (product.LeadTime > 0)
            {
                NotifyDelay(product.LeadTime, product);
            }
        }
    }
}
