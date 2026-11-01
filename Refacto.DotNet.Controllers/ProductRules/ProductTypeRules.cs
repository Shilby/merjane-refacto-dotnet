using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public abstract class ProductTypeRules
    {
        protected readonly INotificationService _notificationService;
        protected readonly AppDbContext _dbContext;

        protected ProductTypeRules(INotificationService notificationService, AppDbContext dbContext)
        {
            _notificationService = notificationService;
            _dbContext = dbContext;
        }

        public abstract void Process(Product product, DateTime now);

        public void NotifyDelay(int leadTime, Product product)
        {
            product.LeadTime = leadTime;
            _ = _dbContext.SaveChanges();
            _notificationService.SendDelayNotification(leadTime, product.Name);
        }

        protected void Sell(Product product)
        {
            product.Sell();
            _ = _dbContext.SaveChanges();
        }
    }
}
