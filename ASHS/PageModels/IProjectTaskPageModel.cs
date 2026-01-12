using ASHS.Models;
using CommunityToolkit.Mvvm.Input;

namespace ASHS.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}