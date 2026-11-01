using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.Services.ProductRules
{
    public class SeasonalProductRules : ProductTypeRules
    {
        public SeasonalProductRules(INotificationService notificationService, AppDbContext dbContext) : base(notificationService, dbContext)
        {
        }

        public override void Process(Product product, DateTime now)
        {
            if (now.Date > product.SeasonStartDate && now.Date < product.SeasonEndDate && product.Available > 0)
            {
                Sell(product);
            }
            else if (now.AddDays(product.LeadTime) > product.SeasonEndDate)
            {
                _notificationService.SendOutOfStockNotification(product.Name);
                product.Available = 0;
                _ = _dbContext.SaveChanges();
            }
            else if (product.SeasonStartDate > now)
            {
                _notificationService.SendOutOfStockNotification(product.Name);
                _ = _dbContext.SaveChanges();
            }
            else
            {
                NotifyDelay(product.LeadTime, product);
            }
        }
    }
}
