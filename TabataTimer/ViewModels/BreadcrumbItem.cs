using System.Windows.Input;
using TabataTimer.Models;

namespace TabataTimer.ViewModels;

public class BreadcrumbItem
{
    public BreadcrumbItem(string name, SequenceFolder? folder, bool isFirst, ICommand command)
    {
        Name = name;
        Folder = folder;
        IsFirst = isFirst;
        NavigateCommand = command;
    }

    public string Name { get; }
    public SequenceFolder? Folder { get; }   // null = root
    public bool IsFirst { get; }
    public ICommand NavigateCommand { get; }
}
