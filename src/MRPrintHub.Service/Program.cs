using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MRPrintHub.Core.Network;
using MRPrintHub.Network;
using MRPrintHub.QR;
using MRPrintHub.Server;
using MRPrintHub.Storage;

namespace MRPrintHub.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IAdapterSource, SystemAdapterSource>();
                services.AddSingleton<INetworkStatusProvider, NetworkStatusProvider>();
                services.AddSingleton<IStorageService, StorageService>();
                services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
                services.AddSingleton<IServerFactory, ServerFactory>();

                services.AddHostedService<ServiceHost>();
            })
            .UseWindowsService();

        var host = builder.Build();
        await host.RunAsync();
    }
}
