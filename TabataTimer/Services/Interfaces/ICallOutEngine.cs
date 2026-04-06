namespace TabataTimer.Services;

public interface ICallOutEngine
{
    string? CurrentExercise { get; }
    void Reset();
    string? Next();
}
