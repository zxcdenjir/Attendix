using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper.Configuration.Attributes;

namespace Attendix.Models;

public partial class StudentsGroups : ObservableObject
{
    [Name("FullName")]
    [ObservableProperty]
    public partial string FullName { get; set; }

    [Name("GroupName")]
    [ObservableProperty]
    public partial string GroupName { get; set; }
}