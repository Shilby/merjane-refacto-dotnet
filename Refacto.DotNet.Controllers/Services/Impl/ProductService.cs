using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services.ProductRules;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class ProductService
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _dbContext;

        public ProductService(INotificationService notificationService, AppDbContext dbContext)
        {
            _notificationService = notificationService;
            _dbContext = dbContext;
        }

        public void ProcessOrderedProduct(Product product, DateTime now)
        {
            ProductTypeRulesFactory.Create(product.Type, _notificationService, _dbContext)?.Process(product, now);
        }

        public void NotifyDelay(int leadTime, Product product)
        {
            new NormalProductRules(_notificationService, _dbContext).NotifyDelay(leadTime, product);
        }
    }
}
