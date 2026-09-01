using Numo.Core.Host.AspNetCore;
using Numo.Core.Host.AspNetCore.Features.Module;
using Numo.Core.Host.AspNetCore.Features.WebApi;
using Numo.Core.Infrastructure.Features.Mediator;
using Numo.Core.Infrastructure.Features.Sql;
using Numo.DevServices.Api;
using Numo.DevServices.Api.Persistence;

var builder = NumoWebApplication.CreateBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddNumo<DevServicesModule>();

    // Discovers handlers and validators by convention in the module's assembly.
    services.AddNumoMediator<DevServicesModule>();

    // Registers the controllers and the global NumoResult-to-HTTP filter.
    services.AddNumoWebApi<DevServicesModule>(_ => { });

    // Npgsql on "DefaultConnection", migration startup, and IUnitOfWork.
    services.AddNumoSql<DevServicesDbContext>();
});

builder.Run();
