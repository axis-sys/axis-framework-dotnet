using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.FileSystem;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisFileSystemStorage(
        this IServiceCollection services,
        FileSystemStorageSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<FileSystemStorageAdapter>();
        services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
        services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
        services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<FileSystemStorageAdapter>());
        return services;
    }

    public static IServiceCollection AddAxisFileSystemStorageFactory(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystemStorageFactory>(sp => new FileSystemStorageFactory(
            sp.GetRequiredService<IAxisMediatorAccessor>()));
        return services;
    }
}
