namespace TabataTimer.Services.Interfaces;

public interface ICallOutEngine
{
    string? CurrentExercise { get; }
    bool CurrentExerciseNeedsMidWorkBeep { get; }
    void Reset();
    string? Next();
}
