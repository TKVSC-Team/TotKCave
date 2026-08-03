namespace TotkCave.PageSource;

public interface IPageSource
{
    byte[] GetPage(int pageFileId);
    string SourceKind { get; }
}
