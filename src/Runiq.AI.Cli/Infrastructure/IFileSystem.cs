namespace Runiq.AI.Cli.Infrastructure;

public interface IFileSystem
{
    void CreateDirectory(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string content);
}

