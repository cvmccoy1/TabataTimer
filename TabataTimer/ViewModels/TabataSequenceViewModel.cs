using TabataTimer.Models;

namespace TabataTimer.ViewModels;

public class TabataSequenceViewModel
{
    private readonly TabataSequence _model;

    public TabataSequenceViewModel(TabataSequence model) => _model = model;

    public Guid Id => _model.Id;
    public string Name => _model.Name;
    public string WaitDisplay => _model.WaitDisplay;
    public string RepeatsDisplay => $"{_model.Repeats}×";
    public string WorkDisplay => _model.WorkDisplay;
    public string RestDisplay => _model.RestDisplay;
    public string TotalDisplay => _model.TotalDisplay;

    public TabataSequence Model => _model;
}
