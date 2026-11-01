using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public class ExpirableProductRules : ProductTypeRules
    {
        public ExpirableProductRules(INotificationService notificationService, AppDbContext dbContext) : base(notificationService, dbContext)
        {
        }

        public override void Process(Product product, DateTime now)
        {
            if (product.Available > 0 && product.ExpiryDate > now.Date)
            {
                Sell(product);
            }
            else
            {
                _notificationService.SendExpirationNotification(product.Name, (DateTime)product.ExpiryDate);
                product.Available = 0;
                _ = _dbContext.SaveChanges();
            }
        }
    }
}
