using Axis;

namespace AxisStorage.FileSystem;

public interface IFileSystemStorageFactory
{
    IAxisStorage Create(FileSystemStorageSettings destination);
}
