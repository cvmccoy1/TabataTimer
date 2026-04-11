using TabataTimer.Models;

namespace TabataTimer.ViewModels;

public class FolderViewModel
{
    public FolderViewModel(SequenceFolder folder) => Folder = folder;

    public Guid Id => Folder.Id;
    public string Name => Folder.Name;
    public SequenceFolder Folder { get; }
    public int ItemCount => Folder.SubFolders.Count + Folder.Sequences.Count;
    public string ItemCountDisplay => ItemCount == 1 ? "1 item" : $"{ItemCount} items";
}
