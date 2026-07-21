using System.Collections.Concurrent;
using Axis;
using AxisMediator.Contracts;

namespace AxisStorage.FileSystem;

internal sealed class FileSystemStorageFactory(IAxisMediatorAccessor accessor) : IFileSystemStorageFactory
{
    private readonly ConcurrentDictionary<string, FileSystemStorageAdapter> _adapters = new();

    public IAxisStorage Create(FileSystemStorageSettings destination)
        => _adapters.GetOrAdd(destination.Root, _ => new FileSystemStorageAdapter(destination, accessor));
}
