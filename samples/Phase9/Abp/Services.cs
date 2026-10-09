using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;

namespace AbpFixture;
public class Book : Entity<Guid> { }
public class Author : Entity<Guid> { }
public interface IBookAppService : IApplicationService { Task<Book> GetAsync(Guid id); }
[ExposeServices(typeof(IBookAppService))]
[Dependency(ServiceLifetime.Scoped, ReplaceServices = true)]
public class BookAppService(IRepository<Book, Guid> repository) : ApplicationService, IBookAppService
{
    [HttpGet("/shared/{id}")]
    public Task<Book> GetAsync(Guid id) => repository.GetAsync(id);
    [RemoteService(false)] public void Hidden() { }
    [RemoteService(IsMetadataEnabled = false)] public void StillRemote() { }
    [NonAction] public void Utility() { }
}
[RemoteService(false)]
public class HiddenAppService : ApplicationService
{
    [RemoteService(true), HttpGet("/must-not-expose")] public void VisibleLooking() { }
}
public class InheritedHiddenAppService : HiddenAppService { public void HiddenToo() { } }
[DisableConventionalRegistration]
public class ManualAppService : ApplicationService { public void Run() { } }
public class GenericUse<T>(IRepository<T, Guid> repository) where T : class, IEntity<Guid>
{
    public Task<T> Get(Guid id) => repository.GetAsync(id);
}
public interface IBookRepository : IRepository<Book, Guid> { }
public class OtherUse(IRepository<Author, Guid> repository)
{
    public Task<Author> Get(Guid id) => repository.GetAsync(id);
}
[RemoteService(false)] public interface IHiddenContract : IApplicationService { void Run(); }
public class InterfaceAttributeAppService : ApplicationService, IHiddenContract { public void Run() { } }
public class AppModule : AbpModule { }
[DependsOn(typeof(AppModule), typeof(AbpAspNetCoreMvcModule))]
public class WebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAspNetCoreMvcOptions>(options => options.ConventionalControllers.Create(typeof(BookAppService).Assembly,
            settings => { settings.RootPath = "catalog"; settings.TypePredicate = type => !type.Name.StartsWith("Manual"); }));
    }
}
public static class RuntimeConfiguration
{
    public static void Configure(AbpAspNetCoreMvcOptions options, Assembly assembly) => options.ConventionalControllers.Create(assembly);
}
